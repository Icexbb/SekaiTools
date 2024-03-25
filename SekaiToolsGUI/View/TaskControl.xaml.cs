using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsCore;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View;

public partial class TaskControl : UserControl
{
    private class LogManager(TaskControl control, TaskControlModel model) : IProgress<TaskLog>
    {
        public void UnexpectedError(Exception e)
        {
            model.ExtraMsg = $"Error: {e.Message}";
            model.Progress = 0;
            model.Running = false;
            model.Status = "运行终止";
        }

        public void Report(TaskLog value)
        {
            switch (value)
            {
                case TaskLogProgress progress:
                    model.Progress = progress.Progress;
                    model.Running = !progress.Finished;
                    model.Status = model.Running ? $"运行中：{model.Progress:0.00}% FPS: {progress.Fps:0.0}" : "运行终止";
                    break;
                case TaskLogContext context:
                    model.ExtraMsg = context.Content;
                    control.Log(context.Content);
                    break;
                case TaskLogRequest request:
                    control.GetSeparatorIndex(request);
                    break;
            }
        }
    }

    private readonly LogManager _logManager;
    private VideoProcess? _videoProcessTask;
    private Task? _task;

    public TaskControl()
    {
        InitializeComponent();
        _logManager = new LogManager(this, (DataContext as TaskControlModel)!);
    }

    private void CreateTask()
    {
        ExpanderLogs.IsExpanded = true;
        ExpanderLogs.Visibility = Visibility.Visible;
        LogViewer.Children.Clear();
        ExpanderSettings.IsExpanded = false;

        var model = (DataContext as TaskControlModel)!.SettingModel;
        var taskConfig = new VideoProcessTaskConfig(
            model.Id, model.VideoFilePath, model.ScriptFilePath, model.TranslateFilePath, model.OutputFilePath);
        taskConfig.SetSubtitleTyperSetting(model.TypewriterChar, model.TypewriterChar);
        _task = new Task(() =>
        {
            try
            {
                _videoProcessTask = new VideoProcess(taskConfig, _logManager);
                var file = _videoProcessTask.Process();
                if (file != "" && File.Exists(file)) OpenFolderAndSelectFile(file);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                _logManager.UnexpectedError(e);
            }
        });
        _task.Start();
        return;

        void OpenFolderAndSelectFile(string fileFullName)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + fileFullName
            };
            System.Diagnostics.Process.Start(psi);
        }
    }

    private void GetSeparatorIndex(TaskLogRequest request)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var (start, end) in request.Collection)
            {
                var frame = TaskSeparatorSelectorWindow.Get(start, end, request.Fps);
                _videoProcessTask!.SetSeparateLine(start, frame);
            }
        });
    }

    private void Log(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            var textBlock = new TextBlock
            {
                Text = DateTime.Now.ToString(@"yyyy/MM/dd | HH:mm:ss | ") + msg,
                TextWrapping = TextWrapping.WrapWithOverflow
            };
            LogViewer.Children.Add(textBlock);
            LogScrollViewer.ScrollToBottom();
        });
    }

    private void StopTask()
    {
        _videoProcessTask?.Stop();
        _videoProcessTask = null;
        _task = null;
    }

    private void ProgressButtonStartTask_OnClick(object sender, RoutedEventArgs e)
    {
        if ((DataContext as TaskControlModel)!.Running)
        {
            HandyControl.Controls.Growl.Error("任务正在运行中");
            return;
        }

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

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopTask();
    }
}