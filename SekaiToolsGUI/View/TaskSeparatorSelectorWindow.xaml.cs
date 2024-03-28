using System.Windows;
using SekaiToolsGUI.ViewModel;
using SekaiToolsCore;

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
            SetPromptWarning();
            SeparateTime = new FrameTimeData(value, new(_fps)).StartTime();
        }
    }

    public string SeparateTime
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    private string Content { get; }

    public int ContentSeparator
    {
        get => GetProperty((Content.Length - 1) / 2);
        set
        {
            SetProperty(value);
            ContentPart1 = Content[..value];
            ContentPart2 = Content[value..];
            SetPromptWarning();
        }
    }

    public int ContentSepLimit => Content.Length - 1;

    public string ContentPart1
    {
        get => GetProperty(Content[..ContentSeparator]);
        private set => SetProperty(value);
    }

    public string ContentPart2
    {
        get => GetProperty(Content[ContentSeparator..]);
        private set => SetProperty(value);
    }

    public string PromptWarning
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    private readonly int _charTime = 80;

    private void SetPromptWarning()
    {
        var frameTime = 1000 / _fps;
        var frameTime1 = (SeparateFrame - StartFrame) * frameTime;
        if (ContentPart1.Length * _charTime > frameTime1)
        {
            PromptWarning = "第一行文字将无法显示完全";
            return;
        }

        var frameTime2 = (EndFrame - SeparateFrame) * frameTime;
        if (ContentPart2.Length * _charTime > frameTime2)
        {
            PromptWarning = "第二行文字将无法显示完全";
            return;
        }

        PromptWarning = "";
    }

    public TaskSeparatorSelectorWindowContext(string content, int start, int end, double fps, int charTime = 80)
    {
        _fps = fps;
        _charTime = charTime;

        StartFrame = start;
        StartInterval = start + 1;
        EndFrame = end;
        EndInterval = end - 1;
        StartTime = new FrameTimeData(start, new(fps)).StartTime();
        EndTime = new FrameTimeData(end, new(fps)).EndTime();

        content = content.Replace("\\N", "");
        int sep;
        if (content.Contains("\\R"))
        {
            sep = content.Split("\\R")[0].Length;
            content = content.Replace("\\R", "");
        }
        else
        {
            sep = content.Length / 2;
        }

        Content = content;
        ContentSeparator = sep;


        Prompt = $"请选择 {StartTime}({StartFrame}) 到 {EndTime}({EndFrame}) 之间的换行信息";
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

    private int ContentSeparator => ((TaskSeparatorSelectorWindowContext)DataContext).ContentSeparator;
    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();

    public TaskSeparatorSelectorWindow(string content, int start, int end, double fps = 60, int charTime = 80)
    {
        InitializeComponent();
        DataContext = new TaskSeparatorSelectorWindowContext(content, start, end, fps, charTime);
    }

    public static List<int> Get(RequestItem item, int charTime = 80)
    {
        var window = new TaskSeparatorSelectorWindow(item.Content, item.StartFrame, item.EndFrame, item.Fps, charTime);
        window.ShowDialog();
        return [window.SepFrameId, window.ContentSeparator];
    }
}