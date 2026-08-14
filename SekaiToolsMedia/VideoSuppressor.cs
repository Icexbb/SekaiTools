using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsMedia;

public sealed partial class VideoSuppressor(IMediaResourceProvider resourceProvider) : IDisposable
{
    private readonly object _runLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Process? _ffmpegProcess;
    private Process? _vapourProcess;
    private Task? _runTask;
    private int _processedFrames;
    private int _totalFrames;
    private double _fps;
    private string _bitrate = "";
    private string _speed = "";
    private string _outputSize = "";
    private string _outputTime = "";
    private VideoSuppressionState _state = VideoSuppressionState.Idle;
    private string _status = "";

    private VideoSuppressionState State
    {
        get
        {
            lock (_runLock) return _state;
        }
        set
        {
            lock (_runLock) _state = value;
        }
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
            _status = "正在取消压制…";
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

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        StopProcess(_vapourProcess);
        StopProcess(_ffmpegProcess);
        _cancellationTokenSource?.Dispose();
        _vapourProcess?.Dispose();
        _ffmpegProcess?.Dispose();
    }

    private async Task RunAsync(VideoSuppressionOptions options, CancellationToken cancellationToken)
    {
        _processedFrames = 0;
        _totalFrames = 0;
        _fps = 0;
        _bitrate = "";
        _speed = "";
        _outputSize = "";
        _outputTime = "";
        _status = "正在分析媒体信息…";
        State = VideoSuppressionState.Preparing;
        PublishProgress();

        VideoOutputTransaction? outputTransaction = null;
        try
        {
            ValidateOptions(options);
            outputTransaction = new VideoOutputTransaction(options.OutputPath, options.OverwriteExisting);
            var ffmpegPath = resourceProvider.GetVapourSynthResourcePath("ffmpeg.exe");
            var audioPlan = await FfmpegAudioInspector
                .InspectAsync(ffmpegPath, options.SourceVideo, cancellationToken)
                .ConfigureAwait(false);
            _status = audioPlan.StreamCount switch
            {
                0 => "未检测到音轨，将仅输出视频",
                _ when audioPlan.CopyAudio => $"检测到 {audioPlan.StreamCount} 条兼容音轨，将全部保留",
                _ => $"检测到 {audioPlan.StreamCount} 条音轨，存在 MP4 不兼容编码，将全部转为 AAC"
            };
            _totalFrames = GetFrameCount(options);
            _vapourProcess = CreateVapourProcess(options);
            _ffmpegProcess = CreateFfmpegProcess(
                options, audioPlan, ffmpegPath, outputTransaction.TemporaryPath);

            if (!_vapourProcess.Start())
                throw new InvalidOperationException("无法启动 VSPipe");
            if (!_ffmpegProcess.Start())
                throw new InvalidOperationException("无法启动 FFmpeg");

            State = VideoSuppressionState.Running;
            PublishProgress();

            await Task.WhenAll(
                TransferPipeAsync(cancellationToken),
                ReadFfmpegLogAsync(cancellationToken),
                _vapourProcess.WaitForExitAsync(cancellationToken),
                _ffmpegProcess.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            if (_vapourProcess.ExitCode != 0)
                throw new InvalidOperationException($"VSPipe 异常退出，退出码: {_vapourProcess.ExitCode}");
            if (_ffmpegProcess.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg 异常退出，退出码: {_ffmpegProcess.ExitCode}");

            outputTransaction.Commit();
            _processedFrames = _totalFrames;
            _status = $"{_status}\n压制完成";
            State = VideoSuppressionState.Completed;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            _status = $"{_status}\n压制已取消";
            State = VideoSuppressionState.Cancelled;
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            State = VideoSuppressionState.Failed;
            _status = string.IsNullOrWhiteSpace(_status)
                ? $"压制失败：{exception.Message}"
                : $"{_status}\n压制失败：{exception.Message}";
            throw;
        }
        finally
        {
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
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
            RedirectStandardOutput = true
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
        string outputPath)
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
        foreach (var argument in BuildFfmpegArguments(options, audioPlan, outputPath))
            startInfo.ArgumentList.Add(argument);
        return new Process { StartInfo = startInfo };
    }

    internal static IReadOnlyList<string> BuildFfmpegArguments(
        VideoSuppressionOptions options,
        FfmpegAudioPlan audioPlan,
        string? outputPath = null)
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

        arguments.Add(outputPath ?? options.OutputPath);
        arguments.Add("-n");
        return arguments;
    }

    private int GetFrameCount(VideoSuppressionOptions options)
    {
        if (options.SourceFrameCount > 0) return options.SourceFrameCount;
        using var capture = new VideoCapture(options.SourceVideo);
        return (int)capture.Get(CapProp.FrameCount);
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

    private async Task ReadFfmpegLogAsync(CancellationToken cancellationToken)
    {
        if (_ffmpegProcess == null) return;
        while (await _ffmpegProcess.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } log)
        {
            UpdateLog(log);
            PublishProgress();
        }
    }

    private void UpdateLog(string log)
    {
        if (TryParseFfmpegProgress(log, out var progress))
        {
            _processedFrames = progress.Frame;
            _fps = progress.FramesPerSecond;
            _bitrate = progress.Bitrate;
            _speed = progress.Speed;
            _outputSize = progress.OutputSize;
            _outputTime = progress.OutputTime;
            var lastLine = _status.Split('\n').LastOrDefault() ?? "";
            if (TryParseFfmpegProgress(lastLine, out _))
                _status = _status[..Math.Max(0, _status.LastIndexOf('\n'))] + "\n" + log;
            else
                _status += "\n" + log;
        }
        else
        {
            _status += "\n" + log;
        }

        _status = _status.Trim();
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
        ProgressChanged?.Invoke(new VideoSuppressionProgress(
            _processedFrames, _totalFrames, _fps, State, _status,
            _bitrate, _speed, _outputSize, _outputTime));
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
