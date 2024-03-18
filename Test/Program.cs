// See https://aka.ms/new-console-template for more information

using SekaiToolsCore;

var config = new VideoProcessTaskConfig("",
    @"D:\ProjectSekai\Archive\event_124_wl_ws\ev_124_01.mp4",
    @"D:\ProjectSekai\Archive\event_124_wl_ws\wl_wonder_01_01.json");
config.SetSubtitleTyperSetting(50, 80);

var task = new VideoProcess(config);

task.Process();