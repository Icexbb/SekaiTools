using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Frame = SekaiToolsCore.Process.Frame;

namespace SekaiToolsGUI.View.Subtitle.Components;

public class DialogLineModel : ViewModelBase
{
    private const int CharTime = 80;
    public readonly DialogFrameSet Set;

    public DialogLineModel(DialogFrameSet set)
    {
        // set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("...", "…");
        Set = set;
        RawContent = set.Data.BodyOriginal;
        TranslatedContent = set.Data.BodyTranslated.EscapedReturn();
        FrameRate = set.Fps;

        UseSeparator = set.NeedSetSeparator;
        if (set.NeedSetSeparator)
        {
            SeparateFrame = set.Separate.SeparateFrame;
            SeparatorContentIndex = set.Separate.SeparatorContentIndex;
        }
    }

    private FrameRate FrameRate { get; }

    public string SpeakerName => Set.Data.CharacterTranslated;

    public string RawContent
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty("");
        set
        {
            SetProperty(value);
            Set.Data.BodyTranslated = value;
        }
    }

    public Visibility ShakeVisibility => Set.Data.Shake ? Visibility.Visible : Visibility.Collapsed;
    public int StartFrame => Set.StartIndex();
    public int EndFrame => Set.EndIndex();
    public string StartTime => FrameRate.TimeAtFrame(StartFrame).GetAssFormatted();
    public string EndTime => FrameRate.TimeAtFrame(EndFrame).GetAssFormatted();

    public bool IsDialogJitter => Set.IsJitter;

    public int SeparatorContentIndexLimit => Set.Data.BodyTranslated.TrimAll().Length - 1;

    public bool UseSeparator
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            Set.UseSeparator = value;
        }
    }

    public int SeparateFrame
    {
        // get => GetProperty(Set.StartIndex());
        get => GetProperty(Set.Separate.SeparatorContentIndex);
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
        get => GetProperty(Set.Separate.SeparatorContentIndex);
        set
        {
            SetProperty(value);
            ContentPart1 = Set.Data.BodyTranslated.TrimAll()[..value];
            ContentPart2 = Set.Data.BodyTranslated.TrimAll()[value..];
            SetPromptWarning();
            Set.SetSeparator(SeparateFrame, SeparatorContentIndex);
        }
    }

    public string ContentPart1
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

    public string ContentPart2
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }


    public string PromptWarning
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }

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
    public DialogLine(DialogFrameSet set)
    {
        set.InitSeparator();
        DataContext = new DialogLineModel(set);
        InitializeComponent();
        CheckLineExpander();
        TextContentPreview.Text = ViewModel.TranslatedContent;
        TextBlockCharacter.Text = ViewModel.Set.Data.CharacterTranslated.Length > 0
            ? ViewModel.Set.Data.CharacterTranslated
            : ViewModel.Set.Data.CharacterOriginal;
    }

    public DialogLineModel ViewModel => (DialogLineModel)DataContext;

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

        var dialog = new QuickEditDialog(ViewModel.Set);

        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var set = ViewModel.Set;
        var edited = dialog.ViewModel.ContentTranslated;
        ViewModel.TranslatedContent = dialog.ViewModel.ContentTranslated;

        DataContext = new DialogLineModel(set);
        ViewModel.UseSeparator = dialog.ViewModel.UseReturn;
        if (edited.Contains('\n'))
        {
            var parts = edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }

        CheckLineExpander();
    }

    private void TextContentPreview_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextContentPreview.Text = ViewModel.RawContent;
    }

    private void TextContentPreview_OnMouseLeave(object sender, MouseEventArgs e)
    {
        TextContentPreview.Text = ViewModel.TranslatedContent;
    }

    private void TextBlockCharacter_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextBlockCharacter.Text = ViewModel.Set.Data.CharacterOriginal;
    }

    private void TextBlockCharacter_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel.Set.Data.CharacterTranslated.Length > 0)
            TextBlockCharacter.Text = ViewModel.Set.Data.CharacterTranslated;
    }
}