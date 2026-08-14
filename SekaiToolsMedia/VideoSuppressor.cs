using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsMedia;

public sealed class VideoSuppressor(IMediaResourceProvider resourceProvider) : IDisposable
{
    private static readonly HashSet<string> FfmpegProgressKeys = new(StringComparer.Ordinal)
    {
        "frame", "fps", "bitrate", "total_size", "out_time_us", "out_time_ms", "out_time",
        "dup_frames", "drop_frames", "speed", "progress"
    };

    private readonly BoundedLineBuffer _detailLog = new(200);
    private readonly Stopwatch _elapsed = new();
    private readonly object _runLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Process? _ffmpegProcess;
    private Process? _vapourProcess;
    private Task? _runTask;
    private int _processedFrames;
    private int _totalFrames;
    private double _fps;
    private string _speed = "";
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
        _speed = "";
        _detailLog.Clear();
        _elapsed.Restart();
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
            var audioStatus = audioPlan.StreamCount switch
            {
                0 => "未检测到音轨，将仅输出视频",
                _ when audioPlan.CopyAudio => $"检测到 {audioPlan.StreamCount} 条兼容音轨，将全部保留",
                _ => $"检测到 {audioPlan.StreamCount} 条音轨，存在 MP4 不兼容编码，将全部转为 AAC"
            };
            _detailLog.Add(audioStatus);
            _status = "正在启动编码器…";
            _totalFrames = GetFrameCount(options);
            _vapourProcess = CreateVapourProcess(options);
            _ffmpegProcess = CreateFfmpegProcess(
                options, audioPlan, ffmpegPath, outputTransaction.TemporaryPath);

            if (!_vapourProcess.Start())
                throw new InvalidOperationException("无法启动 VSPipe");
            if (!_ffmpegProcess.Start())
                throw new InvalidOperationException("无法启动 FFmpeg");

            _status = "正在压制视频";
            State = VideoSuppressionState.Running;
            PublishProgress();

            await Task.WhenAll(
                TransferPipeAsync(cancellationToken),
                ReadFfmpegLogAsync(cancellationToken),
                ReadVapourSynthLogAsync(cancellationToken),
                _vapourProcess.WaitForExitAsync(cancellationToken),
                _ffmpegProcess.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            if (_vapourProcess.ExitCode != 0)
                throw new InvalidOperationException($"VSPipe 异常退出，退出码: {_vapourProcess.ExitCode}");
            if (_ffmpegProcess.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg 异常退出，退出码: {_ffmpegProcess.ExitCode}");

            outputTransaction.Commit();
            _processedFrames = _totalFrames;
            _status = "压制完成";
            State = VideoSuppressionState.Completed;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            _status = "压制已取消";
            State = VideoSuppressionState.Cancelled;
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            State = VideoSuppressionState.Failed;
            _status = $"压制失败：{exception.Message}";
            _detailLog.Add(exception.ToString());
            throw;
        }
        finally
        {
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
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
            "-nostats", "-progress", "pipe:2",
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
        var progressValues = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await _ffmpegProcess.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } log)
        {
            if (!TryParseProgressValue(log, out var key, out var value))
            {
                _detailLog.Add(log);
                continue;
            }

            progressValues[key] = value;
            if (key != "progress") continue;

            ApplyFfmpegProgress(progressValues);
            progressValues.Clear();
            PublishProgress();
        }
    }

    private async Task ReadVapourSynthLogAsync(CancellationToken cancellationToken)
    {
        if (_vapourProcess == null) return;
        while (await _vapourProcess.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } log)
            _detailLog.Add($"[VSPipe] {log}");
    }

    internal static bool TryParseProgressValue(string line, out string key, out string value)
    {
        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            key = "";
            value = "";
            return false;
        }

        key = line[..separator];
        value = line[(separator + 1)..];
        return FfmpegProgressKeys.Contains(key);
    }

    private void ApplyFfmpegProgress(IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue("frame", out var frame) &&
            int.TryParse(frame, NumberStyles.Integer, CultureInfo.InvariantCulture, out var processedFrames))
            _processedFrames = processedFrames;

        if (values.TryGetValue("fps", out var fps) &&
            double.TryParse(fps, NumberStyles.Float, CultureInfo.InvariantCulture, out var framesPerSecond))
            _fps = framesPerSecond;

        if (values.TryGetValue("speed", out var speed))
            _speed = speed;
    }

    private void PublishProgress()
    {
        TimeSpan? estimatedRemaining = State == VideoSuppressionState.Running && _fps > 0 &&
                                           _totalFrames > _processedFrames
            ? TimeSpan.FromSeconds((_totalFrames - _processedFrames) / _fps)
            : null;
        ProgressChanged?.Invoke(new VideoSuppressionProgress(
            _processedFrames, _totalFrames, _fps, State, _status,
            _detailLog.ToString(), _speed, _elapsed.Elapsed, estimatedRemaining));
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

}
