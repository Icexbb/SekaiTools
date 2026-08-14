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
    private bool _running;
    private string _status = "";

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
            _cancellationTokenSource?.Cancel();
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
            runTask = _runTask;
        }

        if (runTask == null) return;
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
        ValidateOptions(options);
        _processedFrames = 0;
        _fps = 0;
        _status = "";
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
        _ffmpegProcess = CreateFfmpegProcess(options, audioPlan, ffmpegPath);
        _running = true;
        PublishProgress();

        try
        {
            if (!_vapourProcess.Start())
                throw new InvalidOperationException("无法启动 VSPipe");
            if (!_ffmpegProcess.Start())
                throw new InvalidOperationException("无法启动 FFmpeg");

            await Task.WhenAll(
                TransferPipeAsync(cancellationToken),
                ReadFfmpegLogAsync(cancellationToken),
                _vapourProcess.WaitForExitAsync(cancellationToken),
                _ffmpegProcess.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            if (_vapourProcess.ExitCode != 0)
                throw new InvalidOperationException($"VSPipe 异常退出，退出码: {_vapourProcess.ExitCode}");
            if (_ffmpegProcess.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg 异常退出，退出码: {_ffmpegProcess.ExitCode}");

            _processedFrames = _totalFrames;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            StopProcess(_vapourProcess);
            StopProcess(_ffmpegProcess);
            _running = false;
            PublishProgress();
            _vapourProcess?.Dispose();
            _ffmpegProcess?.Dispose();
            _vapourProcess = null;
            _ffmpegProcess = null;
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
        string ffmpegPath)
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
        foreach (var argument in BuildFfmpegArguments(options, audioPlan))
            startInfo.ArgumentList.Add(argument);
        return new Process { StartInfo = startInfo };
    }

    internal static IReadOnlyList<string> BuildFfmpegArguments(
        VideoSuppressionOptions options,
        FfmpegAudioPlan audioPlan)
    {
        var arguments = new List<string>
        {
            "-f", "yuv4mpegpipe", "-i", "-", "-i", options.SourceVideo,
            "-map", "0:v:0", "-map", "1:a?", "-c:v", "libx264",
            "-x264-params", options.X264Parameters,
            "-c:a", audioPlan.CopyAudio ? "copy" : "aac"
        };
        if (!audioPlan.CopyAudio)
        {
            arguments.Add("-b:a");
            arguments.Add("192k");
        }

        arguments.Add(options.OutputPath);
        arguments.Add("-y");
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
        var match = FfmpegProgressPattern().Match(log);
        if (match.Success)
        {
            _processedFrames = int.Parse(match.Groups["FrameNumber"].Value, CultureInfo.InvariantCulture);
            _fps = double.Parse(match.Groups["FramesPerSecond"].Value, CultureInfo.InvariantCulture);
            var lastLine = _status.Split('\n').LastOrDefault() ?? "";
            if (FfmpegProgressPattern().IsMatch(lastLine))
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

    private void PublishProgress()
    {
        ProgressChanged?.Invoke(new VideoSuppressionProgress(
            _processedFrames, _totalFrames, _fps, _running, _status));
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

    [GeneratedRegex(@"^frame=\s{0,}(?<FrameNumber>\d*)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)")]
    private static partial Regex FfmpegProgressPattern();
}
