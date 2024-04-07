using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Story.Event;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;
using Frame = SekaiToolsCore.Process.Frame;

namespace SekaiToolsGUI.View.Subtitle;

public class DialogLineModel : ViewModelBase
{
    public readonly DialogFrameSet Set;
    private readonly FrameRate _frameRate;

    public DialogLineModel(DialogFrameSet set)
    {
        Set = set;
        _frameRate = set.Fps;

        if (!NeedSetSeparator) return;

        SeparateFrame = Utils.Middle(set.StartIndex() + 1, set.EndIndex() - 1,
            set.StartIndex() + set.Frames.Count / 2);
        if (set.Data.BodyTranslated.Contains("\\R"))
            SeparatorContentIndex = set.Data.BodyTranslated
                .Replace("\n", "").Replace("\\N", "")
                .IndexOf("\\R", StringComparison.Ordinal);
        else
            SeparatorContentIndex = CleanContent.Length / 2;
    }

    public int Index => Set.Data.Index;
    public string Speaker => Set.Data.CharacterTranslated;

    public string ContentPiece
    {
        get
        {
            var s = CleanContent[..Math.Min(10, CleanContent.Length)];
            if (s.Length < CleanContent.Length) s += "...";
            return s;
        }
    }

    public int StartFrame => Set.StartIndex();
    public string StartTime => _frameRate.TimeAtFrame(StartFrame).GetAssFormatted();
    public int EndFrame => Set.EndIndex();
    public string EndTime => _frameRate.TimeAtFrame(EndFrame).GetAssFormatted();

    public bool IsDialogJitter => Set.IsJitter;

    public string CleanContent => Set.Data.BodyTranslated
        .Replace("\n", "")
        .Replace("\\R", "")
        .Replace("\\N", "");

    public int SeparatorContentIndexLimit => CleanContent.Length - 1;

    public bool NeedSetSeparator =>
        Set.Data.BodyTranslated != string.Empty &&
        Set.Data.BodyOriginal.LineCount() == 3 &&
        Set.Data.BodyTranslated.Replace("\n", "")
            .Replace("\\R", "").Replace("\\N", "")
            .Length > 37;


    public int SeparateFrame
    {
        get => GetProperty(Set.StartIndex());
        set
        {
            SetProperty(value);
            SetPromptWarning();
            SeparateTime = new Frame(value, _frameRate).StartTime();
            Set.SetSeparator(SeparateFrame, SeparatorContentIndex);
        }
    }

    public string SeparateTime
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    public int SeparatorContentIndex
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            ContentPart1 = CleanContent[..value];
            ContentPart2 = CleanContent[value..];
            SetPromptWarning();
            Set.SetSeparator(SeparateFrame, SeparatorContentIndex);
        }
    }

    public string ContentPart1
    {
        get => GetProperty(CleanContent[..SeparatorContentIndex]);
        private set => SetProperty(value);
    }

    public string ContentPart2
    {
        get => GetProperty(CleanContent[SeparatorContentIndex..]);
        private set => SetProperty(value);
    }

    public string PromptWarning
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    private const int CharTime = 80;

    private void SetPromptWarning()
    {
        var frameTime = 1000 / _frameRate.Fps();
        var frameTime1 = (SeparateFrame - Set.StartIndex()) * frameTime;
        if (ContentPart1.Length * CharTime > frameTime1)
        {
            PromptWarning = "第一行文字将无法显示完全";
            return;
        }

        var frameTime2 = (Set.EndIndex() - SeparateFrame) * frameTime;
        if (ContentPart2.Length * CharTime > frameTime2)
        {
            PromptWarning = "第二行文字将无法显示完全";
            return;
        }

        PromptWarning = "";
    }
}

public partial class DialogLine : UserControl, INavigableView<DialogLineModel>
{
    public DialogLineModel ViewModel => (DialogLineModel)DataContext;

    public DialogLine(DialogFrameSet set)
    {
        DataContext = new DialogLineModel(set);
        InitializeComponent();
    }

    private void LineCardExpander_OnLoaded(object sender, RoutedEventArgs e)
    {
        LineCardExpander.IsExpanded = ViewModel.NeedSetSeparator;
    }
}