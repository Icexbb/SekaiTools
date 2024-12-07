using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SekaiToolsCore.Process;

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
        return $"bframes={BFrames}:" +
               $"b-adapt={BAdapt}:" +
               $"me={Me}:" +
               $"merange={MeRange}:" +
               $"subme={SubMe}:" +
               $"aq-mode={AqMode}:" +
               $"ref={Ref}:" +
               $"psy-rd='{PsyRd}':" +
               $"deblock='{DeBlock}':" +
               $"keyint={KeyInt}:" +
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

    private readonly List<Task> _subTasks = [];
    private Process? _fProcess;
    private Process? _vProcess;

    private int FrameCount { get; set; }
    private double Fps { get; set; }
    private bool Running { get; set; }

    private static string VapourExecutable =>
        Path.GetRelativePath(".", ResourceManager.ResourcePath("vapourSynth/VSPipe.exe"));

    private static string VapourScript =>
        Path.GetRelativePath(".", ResourceManager.ResourcePath("vapourSynth/lim5994.vpy"));

    private static string FfmpegExecutable =>
        Path.GetRelativePath(".", ResourceManager.ResourcePath("vapourSynth/ffmpeg.exe"));

    private static bool ScriptExist =>
        File.Exists(VapourScript) && File.Exists(VapourExecutable) && File.Exists(FfmpegExecutable);

    private static bool SourceExist =>
        File.Exists(SuppressPageModel.Instance.SourceVideo) &&
        File.Exists(SuppressPageModel.Instance.SourceSubtitle);


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
               $"-map 0:v -map 1:1 " +
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

    public void Suppress()
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

        _vProcess = GetVapourProcess();
        _fProcess = GetFfmpegProcess();

        _vProcess.Start();
        _fProcess.Start();

        CreateSubTasks();
    }

    private void CreateSubTasks()
    {
        _subTasks.Clear();
        _subTasks.Add(new Task(UpdateLog));
        _subTasks.Add(new Task(TransferPipe));
        _subTasks.ForEach(p => p.Start());
    }

    public void Clean()
    {
        DisposeProcess(_vProcess);
        DisposeProcess(_fProcess);
        _vProcess = null;
        _fProcess = null;

        SuppressPageModel.Instance.ReloadStatus();

        FrameCount = 0;
        Fps = 0;
        Running = false;
        return;

        void DisposeProcess(Process? p)
        {
            if (p == null) return;
            if (!p.HasExited) p.Kill();
            p.Dispose();
        }
    }

    private void TransferPipe()
    {
        if (_vProcess == null || _fProcess == null) return;
        var vapourOut = _vProcess.StandardOutput.BaseStream;
        var ffmpegIn = _fProcess.StandardInput.BaseStream;

        var buffer = new byte[512];
        try
        {
            int byteRead;
            while ((byteRead = vapourOut.Read(buffer, 0, buffer.Length)) > 0)
            {
                ffmpegIn.Write(buffer, 0, byteRead);
            }

            ffmpegIn.Close();
        }
        catch (Exception e)
        {
            SuppressPageModel.Instance.Status += e.Message;
        }
    }

    private void UpdateLog()
    {
        Running = true;
        SuppressPageModel.Instance.ReloadStatus();
        SuppressPageModel.Instance.HasNotStarted = false;

        while (_fProcess is { StandardError.EndOfStream: false })
        {
            var log = _fProcess.StandardError.ReadLine();
            if (log == null) continue;
            AnalysisLog(log);
            UpdateProgression();
        }

        Running = false;
        FrameCount = GetFrameCount();
        UpdateProgression();
    }

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
        @"^frame=\s{0,}(?<FrameNumber>\d*)\s+fps=\s{0,}(?<FramesPerSecond>[\d\.]+)\s+q=(?<QuanitizerScale>[\d\.]+)\s+L?size=\s+(?<Size>\d{1,}\w*B)\s+time=(?<Time>([\d\:\.]+)|(N\/A))\s{0,}bitrate=\s{0,}(?<Bitrate>([\d\.]+kbits\/s?)|(N\/A))\s+speed=\s{0,}(?<Speed>([\d\.]+x)|(N\/A))")]
    private static partial Regex FfmpegProgressPattern();
}