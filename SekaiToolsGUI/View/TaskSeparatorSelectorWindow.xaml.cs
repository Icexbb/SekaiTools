using System.Windows;
using HandyControl.Controls;
using SekaiToolsGUI.ViewModel;
using SekaiToolsCore;
using Window = System.Windows.Window;

namespace SekaiToolsGUI.View;

public class TaskSeparatorSelectorWindowContext
    : ViewModelBase
{
    private readonly double _fps;
    public int StartFrame { get; }
    public int EndFrame { get; }

    public int StartInterval { get; }
    public int EndInterval { get; }

    public string StartTime { get; }
    public string EndTime { get; }

    public string Prompt { get; } = "请选择  到  之间的换行帧ID";

    public int SeparateFrame
    {
        get => GetProperty(StartFrame);
        set
        {
            SetProperty(value);
            SeparateTime = new FrameMatchResult(value).FrameTimeStr(_fps);
        }
    }

    public string SeparateTime
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public TaskSeparatorSelectorWindowContext(int start, int end, double fps)
    {
        _fps = fps;
        StartFrame = start;
        StartInterval = start + 1;
        EndFrame = end;
        EndInterval = end - 1;
        StartTime = new FrameMatchResult(start).FrameTimeStr(fps);
        EndTime = new FrameMatchResult(end).FrameEndTimeStr(fps);
        Prompt = $"请选择 {StartTime}({StartFrame}) 到 {EndTime}({EndFrame}) 之间的换行帧ID";
        SeparateFrame = (start + end) / 2;
    }

    public TaskSeparatorSelectorWindowContext()
    {
        throw new NotImplementedException();
    }
}

public partial class TaskSeparatorSelectorWindow : HandyControl.Controls.Window
{
    private int SepFrameId => ((TaskSeparatorSelectorWindowContext)DataContext).SeparateFrame;
    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    public TaskSeparatorSelectorWindow(int start, int end, double fps = 60)
    {
        InitializeComponent();
        DataContext = new TaskSeparatorSelectorWindowContext(start, end, fps);
    }

    public static int Get(int start, int end, double fps = 60)
    {
        var window = new TaskSeparatorSelectorWindow(start, end, fps);
        window.ShowDialog();
        return window.SepFrameId;
    }
}