using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.Suppress;

public partial class Suppressor
{
    private readonly object _runLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _runTask;
    private Process? _fProcess;
    private Process? _vProcess;
    public static Suppressor Instance { get; } = new();

    private int FrameCount { get; set; }
    private double Fps { get; set; }
    private bool Running { get; set; }

    private static string VapourExecutable =>
        Path.GetRelativePath(".",
            ResourceManager.Instance.ResourcePath(ResourceType.VapourSynth, "VSPipe.exe"));

    private static string VapourScript =>
        Path.GetRelativePath(".",
            ResourceManager.Instance.ResourcePath(ResourceType.VapourSynth, "lim5994.vpy"));

    private static string FfmpegExecutable =>
        Path.GetRelativePath(".",
            ResourceManager.Instance.ResourcePath(ResourceType.VapourSynth, "ffmpeg.exe"));

    private static bool ScriptExist =>
        File.Exists(VapourScript) && File.Exists(VapourExecutable) && File.Exists(FfmpegExecutable);

    private static bool SourceExist =>
        File.Exists(SuppressPageModel.Instance.SourceVideo);


    private static string GetVapourArgs()
    {
        return $"""
                "{VapourScript}" - -c y4m -a "source={SuppressPageModel.Instance.SourceVideo}" -a "subtitle={SuppressPageModel.Instance.SourceSubtitle}"
                """;
    }

    private static Process GetVapourProcess()
    {
        var process = new Process();
        var vapourStartInfo = new ProcessStartInfo
        {
            FileName = VapourExecutable,
            Arguments = GetVapourArgs(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = false
        };
        process.StartInfo = vapourStartInfo;
        return process;
    }

    private static string GetFfmpegArgs()
    {
        var source = SuppressPageModel.Instance.SourceVideo;
        var output = SuppressPageModel.Instance.OutputPath;

        var config = SuppressPageModel.Instance.UseComplexConfig
            ? X264Params.Instance.GetX264Params()
            : X264Params.Instance.GetSimpleX264Params();
        return $"""-f yuv4mpegpipe -i - -i "{source}" """ +
               $"-map 0:v:0 -map 1:a:0 " +
               $"-c:v libx264 -x264-params {config} " +
               $"-c:a copy " +
               $"\"{output}\" " +
               $"-y";
    }

    private static Process GetFfmpegProcess()
    {
        var process = new Process();
        var ffmpegStartInfo = new ProcessStartInfo
        {
            FileName = FfmpegExecutable,
            Arguments = GetFfmpegArgs(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };
        process.StartInfo = ffmpegStartInfo;
        return process;
    }

    private static int GetFrameCount()
    {
        if (SuppressPageModel.Instance.SourceFrameCount != 0) return SuppressPageModel.Instance.SourceFrameCount;
        if (SuppressPageModel.Instance.SourceVideo == "" || !File.Exists(SuppressPageModel.Instance.SourceVideo))
            return SuppressPageModel.Instance.SourceFrameCount;

        using var capture = new VideoCapture(SuppressPageModel.Instance.SourceVideo);
        SuppressPageModel.Instance.SourceFrameCount = (int)capture.Get(CapProp.FrameCount);

        return SuppressPageModel.Instance.SourceFrameCount;
    }

    public Task SuppressAsync()
    {
        lock (_runLock)
        {
            if (_runTask is { IsCompleted: false })
                return _runTask;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _runTask = RunAsync(_cancellationTokenSource.Token);
            return _runTask;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!ScriptExist)
            throw new FileNotFoundException("压制运行环境不完整");

        if (!SourceExist)
            throw new FileNotFoundException("源视频不存在", SuppressPageModel.Instance.SourceVideo);

        _vProcess = GetVapourProcess();
        _fProcess = GetFfmpegProcess();

        SuppressPageModel.Instance.ReloadStatus();
        SuppressPageModel.Instance.HasNotStarted = false;
        Running = true;
        UpdateProgression();
        try
        {
            if (!_vProcess.Start())
                throw new InvalidOperationException("无法启动 VSPipe");
            if (!_fProcess.Start())
                throw new InvalidOperationException("无法启动 FFmpeg");

            var pipeTask = TransferPipeAsync(cancellationToken);
            var logTask = UpdateLogAsync(cancellationToken);
            await Task.WhenAll(pipeTask, logTask);
            await Task.WhenAll(
                _vProcess.WaitForExitAsync(cancellationToken),
                _fProcess.WaitForExitAsync(cancellationToken));

            if (_vProcess.ExitCode != 0)
                throw new InvalidOperationException($"VSPipe 异常退出，退出码: {_vProcess.ExitCode}");
            if (_fProcess.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg 异常退出，退出码: {_fProcess.ExitCode}");

            FrameCount = GetFrameCount();
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        finally
        {
            StopProcess(_vProcess);
            StopProcess(_fProcess);
            Running = false;
            UpdateProgression();
            _vProcess?.Dispose();
            _fProcess?.Dispose();
            _vProcess = null;
            _fProcess = null;
        }
    }

    public void Clean()
    {
        _cancellationTokenSource?.Cancel();
        StopProcess(_vProcess);
        StopProcess(_fProcess);

        SuppressPageModel.Instance.ReloadStatus();

        FrameCount = 0;
        Fps = 0;
        Running = false;
    }

    private static void StopProcess(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* already exited */ }
    }

    public async Task CleanAsync()
    {
        Task? runTask;
        lock (_runLock)
        {
            runTask = _runTask;
        }

        Clean();
        if (runTask != null)
            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // 用户主动停止。
            }

        SuppressPageModel.Instance.ReloadStatus();
    }

    private async Task TransferPipeAsync(CancellationToken cancellationToken)
    {
        if (_vProcess == null || _fProcess == null) return;
        var vapourOut = _vProcess.StandardOutput.BaseStream;
        var ffmpegIn = _fProcess.StandardInput.BaseStream;
        await vapourOut.CopyToAsync(ffmpegIn, cancellationToken);
        await ffmpegIn.FlushAsync(cancellationToken);
        ffmpegIn.Close();
    }

    private async Task UpdateLogAsync(CancellationToken cancellationToken)
    {
        if (_fProcess == null) return;

        while (await _fProcess.StandardError.ReadLineAsync(cancellationToken) is { } log)
        {
            AnalysisLog(log);
            UpdateProgression();
        }
    }

    private void AnalysisLog(string log)
    {
        if (FfmpegProgressPattern().IsMatch(log))
        {
            var match = FfmpegProgressPattern().Match(log);
            FrameCount = int.Parse(match.Groups["FrameNumber"].Value, CultureInfo.InvariantCulture);
            Fps = double.Parse(match.Groups["FramesPerSecond"].Value, CultureInfo.InvariantCulture);

            var lastLine = SuppressPageModel.Instance.Status.Split("\n").Last();
            if (FfmpegProgressPattern().IsMatch(lastLine))
            {
                var str = SuppressPageModel.Instance.Status;
                SuppressPageModel.Instance.Status = string.Concat(str.AsSpan(0,
                    str.LastIndexOf('\n')), "\n", log);
            }
            else
            {
                SuppressPageModel.Instance.Status += "\n" + log;
            }
        }
        else
        {
            SuppressPageModel.Instance.Status += "\n" + log;
        }
    }

    private void UpdateProgression()
    {
        var totalFrames = GetFrameCount();
        SuppressPageModel.Instance.Progression = totalFrames > 0
            ? Math.Clamp((double)FrameCount / totalFrames, 0, 1)
            : 0;
        SuppressPageModel.Instance.Fps = Fps;
        SuppressPageModel.Instance.Running = Running;
    }

    // [GeneratedRegex(@"^frame=\s{0,}(?<FrameNumber>\d*)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)\s+q=(?<QuanitizerScale>[\d\.]+)\s+L?size=\s+(?<Size>\d{1,}\w*B)\s+time=(?<Time>([\d\:\.]+)|(N\/A))\s{0,}bitrate=\s{0,}(?<Bitrate>([\d\.]+kbits\/s?)|(N\/A))\s+speed=\s{0,}(?<Speed>([\d\.]+x)|(N\/A))")]
    [GeneratedRegex(@"^frame=\s{0,}(?<FrameNumber>\d*)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)")]
    private static partial Regex FfmpegProgressPattern();
}
