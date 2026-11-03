using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using SekaiToolsBase;

namespace SekaiToolsMedia;

public sealed partial class VideoSuppressor(IMediaResourceProvider resourceProvider) : IDisposable
{
    private bool _isFinalizing;
    private volatile bool _drainingLogs;
    private readonly Stopwatch _elapsed = new();
    private readonly object _runLock = new();
    private string _bitrate = "";
    private CancellationTokenSource? _cancellationTokenSource;
    private Process? _ffmpegProcess;
    private double _fps;
    private string _outputSize = "";
    private string _outputTime = "";
    private int _processedFrames;
    private Task? _runTask;
    private string _speed = "";
    private VideoSuppressionState _state = VideoSuppressionState.Idle;
    private readonly object _publishLock = new();
    private readonly object _logLock = new();
    private readonly StringBuilder _fullLog = new();
    private string _ffmpegCommand = "";
    private string _latestProgressLine = "";
    private int _totalFrames;
    private Process? _vapourProcess;

    private VideoSuppressionState State
    {
        get
        {
            lock (_runLock)
            {
                return _state;
            }
        }
        set
        {
            lock (_runLock)
            {
                _state = value;
            }
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        StopProcess(_vapourProcess);
        StopProcess(_ffmpegProcess);
        _cancellationTokenSource?.Dispose();
        _vapourProcess?.Dispose();
        _ffmpegProcess?.Dispose();
    }

    public event Action<VideoSuppressionProgress>? ProgressChanged;

    public Task SuppressAsync(VideoSuppressionOptions options, CancellationToken cancellationToken = default)
    {
        lock (_runLock)
        {
            if (_runTask is { IsCompleted: false })
                return _runTask;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = RunAsync(options, _cancellationTokenSource.Token);
            return _runTask;
        }
    }

    public async Task CancelAsync()
    {
        Task? runTask;
        lock (_runLock)
        {
            runTask = _runTask;
            if (runTask is not { IsCompleted: false } ||
                State is not (VideoSuppressionState.Preparing or VideoSuppressionState.Running))
                return;

            State = VideoSuppressionState.Cancelling;
            AppendLog("正在取消压制…");
            _cancellationTokenSource?.Cancel();
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
        }

        PublishProgress();
        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when the user cancels the operation.
        }
    }

    private async Task RunAsync(VideoSuppressionOptions options, CancellationToken cancellationToken)
    {
        _isFinalizing = false;
        _drainingLogs = false;
        _elapsed.Restart();
        _processedFrames = 0;
        _totalFrames = 0;
        _fps = 0;
        _bitrate = "";
        _speed = "";
        _outputSize = "";
        _outputTime = "";
        lock (_logLock) _fullLog.Clear();
        _ffmpegCommand = "";
        _latestProgressLine = "";
        AppendLog("正在分析媒体信息…");
        State = VideoSuppressionState.Preparing;
        PublishProgress();

        VideoOutputTransaction? outputTransaction = null;
        Task? vapourLogTask = null;
        Task? ffmpegLogTask = null;
        try
        {
            ValidateOptions(options);
            Logger.Log($"开始视频压制: 输入={options.SourceVideo}, 输出={options.OutputPath}");
            outputTransaction = new VideoOutputTransaction(options.OutputPath, options.OverwriteExisting);
            var ffmpegPath = resourceProvider.GetVapourSynthResourcePath("ffmpeg.exe");
            var audioPlan = await FfmpegAudioInspector
                .InspectAsync(ffmpegPath, options.SourceVideo, cancellationToken, line => { AppendLog("[媒体检测] " + line); PublishProgress(); })
                .ConfigureAwait(false);
            var audioDescription = audioPlan.StreamCount switch
            {
                0 => "未检测到音轨，将仅输出视频",
                _ when audioPlan.CopyAudio => $"检测到 {audioPlan.StreamCount} 条兼容音轨，将全部保留",
                _ => $"检测到 {audioPlan.StreamCount} 条音轨，存在 MP4 不兼容编码，将全部转为 AAC"
            };
            AppendLog(audioDescription);
            var (frameCount, sourceHeight) = GetVideoInfo(options);
            _totalFrames = frameCount;
            var threads = options.EncodingSettings.GetThreadCount(sourceHeight, Environment.ProcessorCount);
            AppendLog(threads > 0 ? $"编码线程数：{threads}" : "编码线程数：由 x264 自动选择");
            Logger.Log($"压制媒体分析完成: 总帧={frameCount}, {audioDescription}");
            _vapourProcess = CreateVapourProcess(options);
            _ffmpegProcess = CreateFfmpegProcess(
                options, audioPlan, ffmpegPath, outputTransaction.TemporaryPath, sourceHeight);

            _ffmpegCommand = FormatCommand(_ffmpegProcess.StartInfo);
            AppendLog("[FFmpeg 参数] " + _ffmpegCommand);
            AppendLog("[VSPipe 参数] " + FormatCommand(_vapourProcess.StartInfo));
            PublishProgress();

            if (!_vapourProcess.Start())
                throw new InvalidOperationException("无法启动 VSPipe");
            vapourLogTask = ReadProcessLogAsync(_vapourProcess, "VSPipe");
            if (!_ffmpegProcess.Start())
                throw new InvalidOperationException("无法启动 FFmpeg");
            ffmpegLogTask = ReadProcessLogAsync(_ffmpegProcess, "FFmpeg");

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                StopProcess(_vapourProcess);
                StopProcess(_ffmpegProcess);
            });

            State = VideoSuppressionState.Running;
            Logger.Log($"压制进程已启动: VSPipe={_vapourProcess.Id}, FFmpeg={_ffmpegProcess.Id}");
            PublishProgress();

            await Task.WhenAll(
                TransferPipeWithCleanupAsync(cancellationToken),
                ffmpegLogTask,
                vapourLogTask,
                WaitForSuccessfulExitAsync(_vapourProcess, "VSPipe", cancellationToken),
                WaitForSuccessfulExitAsync(_ffmpegProcess, "FFmpeg", cancellationToken)).ConfigureAwait(false);

            _isFinalizing = true;
            PublishProgress();
            outputTransaction.Commit();
            _processedFrames = _totalFrames;
            AppendLog("压制完成");
            State = VideoSuppressionState.Completed;
            Logger.Log($"视频压制完成: 输出={options.OutputPath}, 总帧={_totalFrames}");
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            _drainingLogs = true;
            AppendLog("压制已取消");
            State = VideoSuppressionState.Cancelled;
            Logger.Log($"视频压制已取消: 输出={options.OutputPath}, 已处理帧={_processedFrames}");
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            _drainingLogs = true;
            State = VideoSuppressionState.Failed;
            Logger.Log($"视频压制失败: 输出={options.OutputPath}, {exception}", LogLevel.Error);
            AppendLog($"压制失败：{exception}");
            throw;
        }
        finally
        {
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
            _drainingLogs = true;
            foreach (var reader in new[] { vapourLogTask, ffmpegLogTask })
            {
                if (reader == null) continue;
                try { await reader.ConfigureAwait(false); }
                catch (Exception ex) { AppendLog($"日志读取异常：{ex.Message}"); }
            }
            _elapsed.Stop();
            PublishProgress();
            _vapourProcess?.Dispose();
            _ffmpegProcess?.Dispose();
            _vapourProcess = null;
            _ffmpegProcess = null;
            outputTransaction?.Dispose();
        }
    }

    private void ValidateOptions(VideoSuppressionOptions options)
    {
        options.Validate();

        foreach (var fileName in new[] { "VSPipe.exe", "lim5994.vpy", "ffmpeg.exe" })
            _ = resourceProvider.GetVapourSynthResourcePath(fileName);
    }

    private Process CreateVapourProcess(VideoSuppressionOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = resourceProvider.GetVapourSynthResourcePath("VSPipe.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add(resourceProvider.GetVapourSynthResourcePath("lim5994.vpy"));
        startInfo.ArgumentList.Add("-");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("y4m");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add($"source={options.SourceVideo}");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add($"subtitle={options.SourceSubtitle}");
        return new Process { StartInfo = startInfo };
    }

    private static Process CreateFfmpegProcess(
        VideoSuppressionOptions options,
        FfmpegAudioPlan audioPlan,
        string ffmpegPath,
        string outputPath,
        int sourceHeight)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in BuildFfmpegArguments(options, audioPlan, outputPath, sourceHeight))
            startInfo.ArgumentList.Add(argument);
        return new Process { StartInfo = startInfo };
    }

    internal static IReadOnlyList<string> BuildFfmpegArguments(
        VideoSuppressionOptions options,
        FfmpegAudioPlan audioPlan,
        string? outputPath = null,
        int sourceHeight = 0)
    {
        var arguments = new List<string>
        {
            "-f", "yuv4mpegpipe", "-i", "-", "-i", options.SourceVideo,
            "-map", "0:v:0", "-map", "1:a?", "-c:v", "libx264",
            "-preset", options.EncodingSettings.FfmpegPreset,
            "-crf", options.EncodingSettings.Crf.ToString(CultureInfo.InvariantCulture),
            "-c:a", audioPlan.CopyAudio ? "copy" : "aac"
        };
        if (!audioPlan.CopyAudio)
        {
            arguments.Add("-b:a");
            arguments.Add("192k");
        }

        var threads = options.EncodingSettings.GetThreadCount(
            sourceHeight > 0 ? sourceHeight : options.SourceHeight, Environment.ProcessorCount);
        arguments.Add("-threads:v");
        arguments.Add(threads.ToString(CultureInfo.InvariantCulture));

        arguments.Add(outputPath ?? options.OutputPath);
        arguments.Add("-n");
        return arguments;
    }

    private static (int FrameCount, int Height) GetVideoInfo(VideoSuppressionOptions options)
    {
        if (options.SourceFrameCount > 0 && options.SourceHeight > 0)
            return (options.SourceFrameCount, options.SourceHeight);
        using var capture = new VideoCapture(options.SourceVideo);
        return (options.SourceFrameCount > 0 ? options.SourceFrameCount : (int)capture.Get(CapProp.FrameCount),
            options.SourceHeight > 0 ? options.SourceHeight : (int)capture.Get(CapProp.FrameHeight));
    }

    private async Task WaitForSuccessfulExitAsync(Process process, string name, CancellationToken cancellationToken)
    {
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode == 0) return;
        StopProcess(_vapourProcess);
        StopProcess(_ffmpegProcess);
        throw new InvalidOperationException($"{name} 异常退出，退出码: {process.ExitCode}");
    }

    private async Task TransferPipeWithCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await TransferPipeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
            throw;
        }
    }

    private async Task TransferPipeAsync(CancellationToken cancellationToken)
    {
        if (_vapourProcess == null || _ffmpegProcess == null) return;
        await _vapourProcess.StandardOutput.BaseStream
            .CopyToAsync(_ffmpegProcess.StandardInput.BaseStream, cancellationToken)
            .ConfigureAwait(false);
        await _ffmpegProcess.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        _ffmpegProcess.StandardInput.Close();
    }

    internal static string FormatCommand(ProcessStartInfo startInfo)
    {
        // Display argv faithfully; this string is never used for process execution.
        static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in value)
            {
                if (character == '\\') { slashes++; continue; }
                result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
        return string.Join(" ", new[] { startInfo.FileName }.Concat(startInfo.ArgumentList).Select(Quote));
    }

    private void AppendLog(string line)
    {
        lock (_logLock) _fullLog.AppendLine(line);
    }

    private async Task ReadProcessLogAsync(Process process, string source)
    {
        // Drain to EOF even on cancellation: cancellation kills both processes, closing their streams.
        while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (source == "FFmpeg") UpdateLog(line);
            else AppendLog($"[{source}] {line}");
            if (!_drainingLogs) PublishProgress();
        }
    }

    private void UpdateLog(string log)
    {
        lock (_logLock)
        {
            if (TryParseFfmpegProgress(log, out var progress))
            {
                _processedFrames = progress.Frame;
                _fps = progress.FramesPerSecond;
                _bitrate = progress.Bitrate;
                _speed = progress.Speed;
                _outputSize = progress.OutputSize;
                _outputTime = progress.OutputTime;
                _latestProgressLine = $"[FFmpeg 进度] {log}";
            }
            else _fullLog.AppendLine($"[FFmpeg] {log}");
        }
    }

    internal static bool TryParseFfmpegProgress(string log, out FfmpegProgressValues progress)
    {
        var match = FfmpegProgressPattern().Match(log);
        var parsedFrame = int.TryParse(match.Groups["FrameNumber"].Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var processedFrames);
        var parsedFps = double.TryParse(match.Groups["FramesPerSecond"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var framesPerSecond);
        if (!parsedFrame || !parsedFps)
        {
            progress = default;
            return false;
        }

        progress = new FfmpegProgressValues(
            processedFrames,
            framesPerSecond,
            GetProgressValue(BitratePattern(), log),
            GetProgressValue(SpeedPattern(), log),
            GetProgressValue(SizePattern(), log),
            GetProgressValue(TimePattern(), log));
        return true;
    }

    private static string GetProgressValue(Regex pattern, string log)
    {
        var match = pattern.Match(log);
        return match.Success ? match.Groups["Value"].Value : "";
    }

    private void PublishProgress()
    {
        lock (_publishLock)
        {
            var state = State;
            VideoSuppressionProgress progress;
            lock (_logLock)
                progress = new(_processedFrames, _totalFrames, _fps, state, _fullLog.ToString() + _latestProgressLine,
                    _bitrate, _speed, _outputSize, _outputTime, _elapsed.Elapsed.TotalSeconds, _isFinalizing, _ffmpegCommand);
            ProgressChanged?.Invoke(progress);
        }
    }

    private static void StopProcess(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (Win32Exception)
        {
            // Already exited.
        }
    }

    [GeneratedRegex(@"^\s*frame=\s*(?<FrameNumber>\d+)\s+fps=\s*(?<FramesPerSecond>[\d.]+)")]
    private static partial Regex FfmpegProgressPattern();

    [GeneratedRegex(@"(?:^|\s)bitrate=\s*(?<Value>\S+)")]
    private static partial Regex BitratePattern();

    [GeneratedRegex(@"(?:^|\s)speed=\s*(?<Value>\S+)")]
    private static partial Regex SpeedPattern();

    [GeneratedRegex(@"(?:^|\s)size=\s*(?<Value>\S+)")]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"(?:^|\s)time=\s*(?<Value>\S+)")]
    private static partial Regex TimePattern();
}

internal readonly record struct FfmpegProgressValues(
    int Frame,
    double FramesPerSecond,
    string Bitrate,
    string Speed,
    string OutputSize,
    string OutputTime);
