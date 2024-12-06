using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace SekaiToolsGUI.View.Suppress;

internal class X264Params
{
    public static X264Params Instance { get; } = new();
    public int BFrames { get; set; } = 16;
    public int BAdapt { get; set; } = 2;
    public string Me { get; set; } = "umh";
    public int MeRange { get; set; } = 24;
    public int SubMe { get; set; } = 11;
    public int AqMode { get; set; } = 3;
    public int Ref { get; set; } = 10;
    public string PsyRd { get; set; } = "0.2:0.0";
    public string DeBlock { get; set; } = "1:2";
    public int KeyInt { get; set; } = 600;
    public int Crf { get; set; } = 21;

    public string GetX264Params()
    {
        return $"bframes={BFrames}:b-adapt={BAdapt}:" +
               $"me={Me}:merange={MeRange}:" +
               $"subme={SubMe}:aq-mode={AqMode}:" +
               $"ref={Ref}:psy-rd={PsyRd}:" +
               $"deblock={DeBlock}:keyint={KeyInt}:" +
               $"crf={Crf}";
    }

    public string GetSimpleX264Params()
    {
        return $"psy-rd={PsyRd}:crf={Crf}";
    }
}

public partial class Suppressor
{
    public static Suppressor Instance { get; } = new();
    private static string VapourExecutable => Path.GetRelativePath(".", "./vs/VSPipe.exe");
    private static string VapourScript => Path.GetRelativePath(".", "./vs/lim5994.vpy");
    private static string FfmpegExecutable => Path.GetRelativePath(".", "./vs/ffmpeg.exe");

    private static bool ScriptExist =>
        File.Exists(VapourScript) && File.Exists(VapourExecutable) && File.Exists(FfmpegExecutable);

    private static bool SourceExist =>
        File.Exists(SuppressPageModel.Instance.SourceVideo) &&
        File.Exists(SuppressPageModel.Instance.SourceSubtitle);

    private static string GetCommand()
    {
        var source = SuppressPageModel.Instance.SourceVideo;
        var subtitle = SuppressPageModel.Instance.SourceSubtitle;
        var output = Path.Join(Path.GetDirectoryName(subtitle),
            Path.GetFileNameWithoutExtension(subtitle) + "_h264.mp4");

        var config = SuppressPageModel.Instance.UseComplexConfig
            ? X264Params.Instance.GetX264Params()
            : X264Params.Instance.GetSimpleX264Params();
        var vapourCommand =
            $"""
             {VapourExecutable} "{VapourScript}" - -c y4m -a "source={source}" -a "subtitle={subtitle}"
             """;
        var ffmpegCommand =
            @$"{FfmpegExecutable} -f yuv4mpegpipe -i - -i ""{source}"" " +
            $"-map 0:v -map 1:1 " +
            $"-c:v libx264 -x264-params {config} " +
            $"-c:a copy " +
            $"\"{output}\" " +
            $"-y";

        var command = vapourCommand + " | " + ffmpegCommand;

        return command;
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

    private Process? _process;

    public async Task Suppress()
    {
        if (!ScriptExist)
        {
            SuppressPageModel.Instance.Status = "Script not found";
            return;
        }

        if (!SourceExist)
        {
            SuppressPageModel.Instance.Status = "Source not found";
            return;
        }

        var command = GetCommand();

        _process = new Process();
        ;
        _process.StartInfo.FileName = "cmd.exe";
        _process.StartInfo.Arguments = "/C " + command;
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.CreateNoWindow = true;

        _process.StartInfo.RedirectStandardInput = false; //不重定向输入
        _process.StartInfo.RedirectStandardOutput = false; //重定向输出
        _process.StartInfo.RedirectStandardError = true; //重定向输出
        _process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        _process.Start();

        var updateLog = new Task(() =>
        {
            Running = true;
            SuppressPageModel.Instance.ReloadStatus();
            SuppressPageModel.Instance.HasNotStarted = false;

            while (_process is { HasExited: false })
            {
                var log = _process.StandardError.ReadLine();
                if (log == null) continue;
                AnalysisLog(log);
                UpdateProgression();
            }

            Running = false;
            FrameCount = GetFrameCount();
            UpdateProgression();
        });

        updateLog.Start();
        await _process.WaitForExitAsync();
    }

    public void Clean()
    {
        if (_process == null) return;
        if (!_process.HasExited)
        {
            _process?.Kill();
        }

        _process?.Dispose();
        _process = null;
        SuppressPageModel.Instance.ReloadStatus();
    }

    private int FrameCount { get; set; } = 0;
    private double Fps { get; set; } = 0;
    private bool Running { get; set; } = false;


    private void AnalysisLog(string log)
    {
        if (FfmpegProgressPattern().IsMatch(log))
        {
            var match = FfmpegProgressPattern().Match(log);
            FrameCount = int.Parse(match.Groups["FrameNumber"].Value);
            Fps = double.Parse(match.Groups["FramesPerSecond"].Value);

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
        SuppressPageModel.Instance.Progression = (double)FrameCount / GetFrameCount();
        SuppressPageModel.Instance.Fps = Fps;
        SuppressPageModel.Instance.Running = Running;
    }

    [GeneratedRegex(
        @"^frame=\s+(?<FrameNumber>\d+)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)\s+q=(?<QuanitizerScale>[\d\.]+)\s+L?size=\s+(?<Size>\d{1,}\w*B)\s+time=(?<Time>[\d\:\.]+)\s{0,}bitrate=\s{0,}(?<Bitrate>[\d\.]+)kbits\/s\s{0,}speed=\s{0,}(?<Speed>[\d\.]+)x\s{0,}")]
    private static partial Regex FfmpegProgressPattern();
}