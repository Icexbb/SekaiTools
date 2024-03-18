using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsCore;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View;

public partial class TaskControl : UserControl
{
    private class LogManager(TaskControlModel model) : IProgress<VideoProcess.ProcessProgressInfo>
    {
        public void Report(VideoProcess.ProcessProgressInfo value)
        {
            if (!model.Running) model.Running = true;
            if (value.Content != "") Console.WriteLine(value.Content);
            model.Progress = value.Progress;
            if (value.ProgressedFrameCount == value.TotalFrameCount && model.Running)
                model.Running = false;
            if (value.OnlyContent) return;

            model.Status = model.Running ? $"运行中：{model.Progress:0.##}%" : "完成";
        }
    }

    private LogManager _logManager;
    private VideoProcess? _videoProcessTask;
    private Task? _task;

    public TaskControl()
    {
        InitializeComponent();
        _logManager = new LogManager((DataContext as TaskControlModel)!);
    }

    private void CreateTask()
    {
        var model = (DataContext as TaskControlModel)!.SettingModel;
        var taskConfig = new VideoProcessTaskConfig(
            model.Id, model.VideoFilePath, model.ScriptFilePath, model.TranslateFilePath, model.OutputFilePath);
        taskConfig.SetSubtitleTyperSetting(model.TypewriterChar, model.TypewriterChar);
        _videoProcessTask = new VideoProcess(taskConfig, _logManager);
        _task = new Task(() => { _videoProcessTask.Process(); });
        _task.Start();
    }

    private void ProgressButtonStartTask_OnClick(object sender, RoutedEventArgs e)
    {
        var model = (DataContext as TaskControlModel)!.SettingModel;
        var vfp = model.VideoFilePath;
        var sfp = model.ScriptFilePath;
        var tfp = model.TranslateFilePath;


        if (vfp.Length == 0)
        {
            HandyControl.Controls.Growl.Error("视频文件不能为空");
            return;
        }

        if (!File.Exists(vfp))
        {
            HandyControl.Controls.Growl.Error("视频文件不存在");
            return;
        }

        if (sfp.Length == 0)
        {
            HandyControl.Controls.Growl.Error("剧情脚本文件不能为空");
            return;
        }

        if (!File.Exists(sfp))
        {
            HandyControl.Controls.Growl.Error("剧情脚本文件不存在");
            return;
        }

        if (tfp.Length == 0)
        {
            var result = HandyControl.Controls.MessageBox.Show("剧情翻译文件为空，是否继续？", "警告", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No) return;
        }
        else if (!File.Exists(tfp))
        {
            HandyControl.Controls.Growl.Error("剧情翻译文件不存在");
            return;
        }

        CreateTask();
    }
}