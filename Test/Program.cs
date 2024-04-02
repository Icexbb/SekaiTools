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

const string
    vfp = @"D:\ProjectSekai\test\aprilfool_2024_01.mp4"; 
const string sfp = @"D:\ProjectSekai\test\aprilfool_2024_01.json";
const string tfp = @"D:\ProjectSekai\test\aprilfool_2024_01.txt";

var vc = new VideoCapture(vfp);
Console.WriteLine(vc.Get(CapProp.Fps));
var vInfo = new VideoInfo(vfp);
Console.WriteLine(vInfo.Fps.Fps());
Console.WriteLine(vInfo.Resolution);
// var config = new Config(vfp, sfp, tfp);
// var task = new VideoProcess(config);
// task.Process();
