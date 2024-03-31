// See https://aka.ms/new-console-template for more information

using System.Drawing;
using SekaiToolsCore;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.Util;
using Newtonsoft.Json.Linq;
using SekaiToolsCore.Process;

const string
    vfp = @"C:\Users\icexb\Downloads\an125-01.mp4"; //@"D:\ProjectSekai\Archive\event_124_wl_ws\ev_124_01.mp4";
const string sfp = @"C:\Users\icexb\Downloads\010033_an01.asset";
const string tfp = @"C:\Users\icexb\Downloads\【翻译】event125-an 前篇 (1).txt";

// var config = new Config(vfp, sfp, tfp);
// var task = new VideoProcess(config);
// task.Process();


var jsonStr = @"[{
      ""id"": 1,
      ""areaId"": 4,
      ""isNextGrade"": false,
      ""scriptId"": ""as_cdshop_mob"",
      ""characterIds"": [1],
      ""archiveDisplayType"": ""none"",
      ""archivePublishedAt"": 1233284400000,
      ""releaseConditionId"": 1
    }]";
var obj = JObject.Parse(jsonStr);

Console.WriteLine(obj.GetType());
