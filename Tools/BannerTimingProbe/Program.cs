using System.Reflection;
using System.Text.Json;
using Emgu.CV;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

if (args.Length < 3 || args.Length % 2 != 1)
    throw new ArgumentException("BannerTimingProbe <video> <start-frame> <end-frame> [<start-frame> <end-frame> ...]");
CvInvoke.NumThreads = 4;
var video = Path.GetFullPath(args[0]);
using var info = new VideoCapture(video);
var fps = info.Get(Emgu.CV.CvEnum.CapProp.Fps);
var type = typeof(BannerBaseFrameSet).Assembly.GetType("SekaiToolsCore.Match.TemplateMatcher.BannerTimingRefiner", true)!;
using var refiner = (IDisposable)Activator.CreateInstance(type, video, fps)!;
for (var i = 1; i < args.Length; i += 2)
{
    var start = int.Parse(args[i]);
    var end = int.Parse(args[i + 1]);
    var set = new BannerBaseFrameSet(new BannerStoryEvent("probe", 0), new FrameRate(fps));
    set.Add(start);
    set.Add(end);
    type.GetMethod("Refine")!.Invoke(refiner, [set, CancellationToken.None]);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        Original = new { Start = start, End = end },
        set.HasRefinedTiming,
        Mask = new { Start = set.StartIndex(), End = set.EndIndex(), StartTime = set.StartTime(), EndTime = set.EndTime() },
        Text = new
        {
            Start = set.StartIndex() + set.TextStartOffset,
            End = set.EndIndex() - set.TextEndOffset,
            StartTime = set.TextStartTime(),
            EndTime = set.TextEndTime()
        }
    }));
}
