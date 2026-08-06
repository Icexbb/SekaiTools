using System.Windows;
using System.Windows.Media;
using SekaiToolsBase.Utils;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class DialogLineModel : ViewModelBase
{
    public readonly DialogBaseFrameSet Set;
    private readonly int _charTime;
    private readonly SpeakerColorPalette? _speakerPalette;

    public DialogLineModel(DialogBaseFrameSet set, int charTime = 80)
    {
        // set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("...", "…");
        Set = set;
        _speakerPalette = SpeakerColorConfig.Get(set.Data.CharacterId);
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

    public int EventIndex => Set.Data.EffectiveStoryIndex;
    public string EventNumber => $"#{EventIndex + 1}";
    public int SpeakerId => Set.Data.CharacterId;
    public string SpeakerName => Set.Data.FinalCharacter;
    public string SpeakerOriginalName => Set.Data.CharacterOriginal;
    public string SpeakerTranslatedName => Set.Data.CharacterTranslated;
    public Visibility SpeakerOriginalNameVisibility =>
        !string.IsNullOrWhiteSpace(SpeakerTranslatedName) &&
        !string.Equals(SpeakerTranslatedName, SpeakerOriginalName, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    public Brush? SpeakerBrush => _speakerPalette?.Background;
    public Brush? SpeakerForegroundBrush => _speakerPalette?.Foreground;
    public bool HasSpeakerColor => _speakerPalette is not null;

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
    public string EventDuration =>
        $"{Math.Max(0, FrameRate.TimeAtFrame(EndFrame).Milliseconds - FrameRate.TimeAtFrame(StartFrame).Milliseconds) / 1000d:0.0}s";

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
        get => GetProperty(Set.Separate.SeparateFrame);
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

    public void RefreshTiming()
    {
        if (Set.UseSeparator)
        {
            SetProperty(Set.Separate.SeparateFrame, nameof(SeparateFrame));
            SeparateTime = new ProcessFrame(Set.Separate.SeparateFrame, FrameRate).StartTime();
        }
        OnPropertyChanged(nameof(StartFrame));
        OnPropertyChanged(nameof(EndFrame));
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(EndTime));
        OnPropertyChanged(nameof(EventDuration));
        OnPropertyChanged(nameof(SeparateFrame));
        OnPropertyChanged(nameof(SeparateTime));
        SetPromptWarning();
    }

    private void SetPromptWarning()
    {
        PromptWarning = string.Join("；", DialogTimingCheck.GetIssues(Set, _charTime).Select(x => x.Warning));
    }
}
