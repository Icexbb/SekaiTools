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

const string vfp = @"D:\ProjectSekai\Archive\126\126-07.mp4";
const string sfp = @"D:\ProjectSekai\Archive\126\126-07.json";
const string tfp = @"D:\ProjectSekai\Archive\126\126-07.txt";

var videoCapture = new VideoCapture(vfp);
var matcherCreator = new MatcherCreator(vfp, sfp, tfp);
var dialogMatcher = matcherCreator.DialogMatcher();

for (var i = 0; i < 53; i++)
{
    dialogMatcher.Set[i].Finished = true;
}

videoCapture.Set(CapProp.PosFrames, 17275);

var frame = new Mat();
while (true)
{
    if (!videoCapture.Read(frame)) break;
    CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);

    var frameIndex = (int)videoCapture.Get(CapProp.PosFrames);

    if (!dialogMatcher.Finished)
    {
        var dialogIndex = dialogMatcher.LastNotProcessedIndex();
        var r = dialogMatcher.Process(frame, frameIndex);
        Console.WriteLine($"Dialog {dialogIndex} processed at frame {frameIndex} with result {r}");


        if (dialogMatcher.Set[dialogIndex].Finished)
            Console.WriteLine($"Dialog {dialogIndex} finished at frame {frameIndex}");
    }
}