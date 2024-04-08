// See https://aka.ms/new-console-template for more information

using System.Drawing;
using SekaiToolsCore;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SekaiToolsCore.Process;
using Action = SekaiToolsCore.Story.Fetch.Data.Action;

const string vfp = @"D:\ProjectSekai\test\aprilfool_2024_01.mp4";
const string sfp = @"D:\ProjectSekai\test\aprilfool_2024_01.json";
const string tfp = @"D:\ProjectSekai\test\aprilfool_2024_01.txt";

var videoCapture = new VideoCapture(vfp);
var matcherCreator = new MatcherCreator(vfp, sfp, tfp);
var dialogMatcher = matcherCreator.DialogMatcher();
var bannerMatcher = matcherCreator.BannerMatcher();
var contentMatcher = matcherCreator.ContentMatcher();


var frame = new Mat();
while (true)
{
    if (
        bannerMatcher.Finished
        // && dialogMatcher.Finished
        && contentMatcher.Finished
    ) break;
    if (!videoCapture.Read(frame)) break;
    CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);

    var frameIndex = (int)videoCapture.Get(CapProp.PosFrames);

    if (!contentMatcher.Finished)
    {
        contentMatcher.Process(frame);
        if (contentMatcher.Finished)
        {
            Console.WriteLine($"Content Start At {frameIndex}");
        }
        else
        {
            continue;
        }
    }

    var matchBanner = true;
    // if (!dialogMatcher.Finished)
    // {
    //     var dialogIndex = dialogMatcher.LastNotProcessedIndex();
    //     var r = dialogMatcher.Process(frame, frameIndex);
    //     if (r) matchBanner = false;
    //
    //     if (dialogMatcher.Set[dialogIndex].Finished)
    //         Console.WriteLine($"Dialog {dialogIndex} finished at frame {frameIndex}");
    // }

    if (matchBanner)
    {
        if (!bannerMatcher.Finished)
        {
            var bannerIndex = bannerMatcher.LastNotProcessedIndex();
            bannerMatcher.Process(frame, frameIndex);
            if (bannerMatcher.Set[bannerIndex].Finished)
                Console.WriteLine($"Banner {bannerIndex} finished at frame {frameIndex}");
        }
    }
}

videoCapture.Dispose();

Console.WriteLine("Finished");

var maker = matcherCreator.SubtitleMaker();
var sub = maker.Make([], bannerMatcher.Set);
Console.WriteLine(sub.ToString());