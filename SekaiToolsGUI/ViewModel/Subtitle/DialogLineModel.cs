using System.Windows;
using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class DialogLineModel : ViewModelBase
{
    public readonly DialogBaseFrameSet Set;
    private readonly int _charTime;

    public DialogLineModel(DialogBaseFrameSet set, int charTime = 80)
    {
        // set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("...", "…");
        Set = set;
        RawContent = set.Data.BodyOriginal;
        TranslatedContent = set.Data.BodyTranslated.EscapedReturn();
        FrameRate = set.Fps;
        _charTime = charTime;

        UseSeparator = set.NeedSetSeparator;
        if (set.NeedSetSeparator)
        {
            SeparateFrame = set.Separate.SeparateFrame;
            SeparatorContentIndex = set.Separate.SeparatorContentIndex;
        }

        SetPromptWarning();
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
            SetPromptWarning();
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
            SeparateTime = new ProcessFrame(value, FrameRate).StartTime();
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
        PromptWarning = string.Join("；", DialogTimingCheck.GetIssues(Set, _charTime).Select(x => x.Warning));
    }
}