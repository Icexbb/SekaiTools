// See https://aka.ms/new-console-template for more information

using SekaiToolsCore;
using Emgu.CV;
using Emgu.CV.CvEnum;

const string
    vfp = @"D:\ProjectSekai\Archive\event_124_wl_ws\ev_124_01.mp4"; // @"C:\Users\icexb\Downloads\an125-01.mp4";
const string sfp = @"C:\Users\icexb\Downloads\010033_an01.asset";
const string tfp = @"C:\Users\icexb\Downloads\【翻译】event125-an 前篇 (1).txt";


double[] f = [25, 29.97, 30, 45, 59.94, 60, 75];
foreach (var d in f) Create($@"D:\ProjectSekai\test\{d}.ass", d);

return;

void Create(string path, double fps)
{
    var result = new SubtitleEvents();
    var fr = new FrameRate(fps);
    for (var i = 0; i < fps * 5; i++)
    {
        var index = i;
        var startTimeSpan = fr.TimeAtFrame(i, FrameType.Start).GetAssFormatted();
        var endTimeSpan = fr.TimeAtFrame(i, FrameType.End).GetAssFormatted();
        result.Add(SubtitleEventItem.Dialog($"Frame {i} {startTimeSpan} -> {endTimeSpan}",
            startTimeSpan, endTimeSpan, "Default"));
    }

    var sub = new Subtitle(
        new SubtitleScriptInfo(1920, 1080),
        new SubtitleGarbage($"?dummy:{fps:00.00000}:{fps * 10:0}:1920:1080:47:163:254:"),
        new SubtitleStyles([new SubtitleStyleItem()]),
        result
    );
    sub.Save(path);
}