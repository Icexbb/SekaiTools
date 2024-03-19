// See https://aka.ms/new-console-template for more information

using SekaiToolsCore;

var config = new VideoProcessTaskConfig("",
    @"D:\ProjectSekai\Archive\event_124_wl_ws\ev_124_01.mp4",
    @"D:\ProjectSekai\Archive\event_124_wl_ws\wl_wonder_01_01.json",
    @"D:\ProjectSekai\Archive\event_124_wl_ws\【合意】wonder-01-01 冒险开幕.txt");
config.SetSubtitleTyperSetting(50, 80);

var task = new VideoProcess(config);

task.Process();