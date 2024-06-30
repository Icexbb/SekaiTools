using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsCore.Story.Event;
using Wpf.Ui.Controls;
using Frame = SekaiToolsCore.Process.Frame;

namespace SekaiToolsGUI.View.Subtitle;

public class DialogLineModel : ViewModelBase
{
    public readonly DialogFrameSet Set;

    private FrameRate FrameRate { get; set; }

    public DialogLineModel(DialogFrameSet set)
    {
        set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("...", "…");
        Set = set;
        RawContent = set.Data.BodyOriginal;
        TranslatedContent = set.Data.BodyTranslated
            .Replace("\\N", "\n")
            .Replace("\\R", "\n");
        FrameRate = set.Fps;

        UseSeparator = NeedSetSeparator;
        if (!NeedSetSeparator) return;

        SeparateFrame = Utils.Middle(set.StartIndex() + 1, set.EndIndex() - 1,
            set.StartIndex() + set.Frames.Count / 2);
        if (set.Data.BodyTranslated.Contains("\\R"))
        {
            SeparatorContentIndex = set.Data.BodyTranslated
                .Replace("\n", "").Replace("\\N", "")
                .IndexOf("\\R", StringComparison.Ordinal);
        }
        else if (set.Data.BodyTranslated.Count(c => c == '\n') == 1)
        {
            SeparatorContentIndex = set.Data.BodyTranslated
                .IndexOf("\\R", StringComparison.Ordinal);
        }
        else
        {
            SeparatorContentIndex = CleanContent.Length / 2;
        }
    }

    public string SpeakerName => Set.Data.CharacterTranslated;


    public string RawContent
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public int StartFrame => Set.StartIndex();
    public int EndFrame => Set.EndIndex();
    public string StartTime => FrameRate.TimeAtFrame(StartFrame).GetAssFormatted();
    public string EndTime => FrameRate.TimeAtFrame(EndFrame).GetAssFormatted();

    public bool IsDialogJitter => Set.IsJitter;

    private string CleanContent => Set.Data.BodyTranslated
        .Replace("\n", "")
        .Replace("\\R", "")
        .Replace("\\N", "");

    public int SeparatorContentIndexLimit => CleanContent.Length - 1;

    private bool NeedSetSeparator =>
        Set.Data.BodyTranslated != string.Empty &&
        Set.Data.BodyOriginal.LineCount() == 3 &&
        Set.Data.BodyTranslated.Replace("\n", "")
            .Replace("\\R", "")
            .Replace("\\N", "")
            .Length > 37;

    public bool UseSeparator
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public int SeparateFrame
    {
        get => GetProperty(Set.StartIndex());
        set
        {
            SetProperty(value);
            SetPromptWarning();
            SeparateTime = new Frame(value, FrameRate).StartTime();
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
        var frameTime = 1000 / FrameRate.Fps();
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
        CheckLineExpander();
    }

    private void CheckLineExpander()
    {
        Dispatcher.Invoke(() =>
        {
            PanelSeparator.Visibility = ViewModel.UseSeparator ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async void DialogLine_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new QuickEditDialog(ViewModel.Set.Data);

        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var set = ViewModel.Set;
        var edited = dialog.ViewModel.ContentTranslated;
        set.Data.SetTranslationContent(edited);


        DataContext = new DialogLineModel(set);
        ViewModel.UseSeparator = dialog.ViewModel.UseReturn;
        if (edited.Contains('\n'))
        {
            var parts = edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }

        CheckLineExpander();
    }
}