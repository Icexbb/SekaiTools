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

var frame = new Mat();
while (true)
{
    if (!videoCapture.Read(frame)) break;
    CvInvoke.CvtColor(frame, frame, ColorConversion.Bgr2Gray);

    var frameIndex = (int)videoCapture.Get(CapProp.PosFrames);

    if (!dialogMatcher.Finished)
    {
        // var dialogIndex = dialogMatcher.LastNotProcessedIndex();
        dialogMatcher.Process(frame, frameIndex);
    }
}