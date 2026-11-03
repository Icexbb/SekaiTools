namespace SekaiToolsGUI.ViewModel.Translate;

public abstract class LineModel : ViewModelBase
{
    public TranslateItemModel Body => this switch
    {
        LineDialogModel dialog => dialog.Content,
        LineEffectModel effect => effect.Content,
        _ => throw new InvalidOperationException("未知翻译条目")
    };

    public virtual bool HasVoice => false;

    public string VoiceState
    {
        get => GetProperty("此条目无原始语音");
        set => SetProperty(value);
    }

    public bool VoiceLoading
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool VoicePlaybackAvailable
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public double VoiceProgress
    {
        get => GetProperty(0d);
        set => SetProperty(value);
    }

    public bool VoicePlaying
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public int SequenceNumber { get; set; }
    public string Label => $"第 {SequenceNumber} 条 · {(this is LineDialogModel ? "对话" : "场景文字")}";
    public int MatchingCount { get; set; } = 1;

    public string SyncHint =>
        this is LineDialogModel ? $"同名角色共 {MatchingCount} 处，同步由项目开关控制" : $"相同场景文字共 {MatchingCount} 处";

    public bool HighlightReferenceDifferences
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool HighlightModifiedDifferences
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ShowReference
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool IsUntranslated => string.IsNullOrWhiteSpace(Body.Translated) && !Body.PreserveEmpty;
    public bool IsExplicitEmpty => string.IsNullOrWhiteSpace(Body.Translated) && Body.PreserveEmpty;
    public bool HasWarning => this is LineDialogModel dialog && !string.IsNullOrEmpty(dialog.Check);

    public string StateText => IsUntranslated ? "未译 · 导出沿用原文" :
        IsExplicitEmpty ? "有意留空" :
        Body.Translated == Body.Original ? "沿用原文" : "已有译文";

    public abstract string Result { get; }

    public int Index
    {
        get => GetProperty(-1);
        set => SetProperty(value);
    }

    public event EventHandler<string>? ActionRequested;

    public void RequestAction(string action)
    {
        ActionRequested?.Invoke(this, action);
    }

    public void NotifyContentState()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsUntranslated));
        OnPropertyChanged(nameof(IsExplicitEmpty));
        OnPropertyChanged(nameof(HasWarning));
    }
}