using System.ComponentModel;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;
using SekaiToolsBase.Story.Translation;
using SekaiToolsConfiguration;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class LineDialogModel : LineModel
{
    private bool _isFormatting;
    public LineDialogModel(DialogStoryEvent dialogStoryEvent)
    {
        EndLine = dialogStoryEvent.CloseWindow;

        CharacterId = dialogStoryEvent.CharacterId;

        Character.Original = dialogStoryEvent.CharacterOriginal;
        Character.PreserveEmpty = Content.PreserveEmpty = dialogStoryEvent.HasExplicitTranslation;
        Character.Translated = !dialogStoryEvent.HasExplicitTranslation && string.IsNullOrWhiteSpace(dialogStoryEvent.CharacterTranslated)
            ? dialogStoryEvent.CharacterOriginal
            : dialogStoryEvent.CharacterTranslated;


        Content.Original = dialogStoryEvent.BodyOriginal;
        Content.Translated = dialogStoryEvent.BodyTranslated;

        Index = dialogStoryEvent.Index;

        Content.PropertyChanged += OnContentPropertyChanged;
        Character.PropertyChanged += OnCharacterPropertyChanged;
        UpdateMetrics();
    }

    public bool EndLine
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public TranslateItemModel Character
    {
        get => GetProperty(new TranslateItemModel());
        set => SetProperty(value);
    }

    public TranslateItemModel Content
    {
        get => GetProperty(new TranslateItemModel());
        set => SetProperty(value);
    }


    public int MaxLineLength
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public bool TooLong
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string Check
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public bool CharacterTranslateChangedEnabled { get; set; } = true;
    public bool ContentTranslateChangedEnabled { get; set; } = true;

    private void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 只有当 Translated 变化时才触发
        if (e.PropertyName != nameof(TranslateItemModel.Translated)) return;
        // 更新父级的 Check, LineCount 等
        UpdateMetrics();
        if (ContentTranslateChangedEnabled) ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCharacterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 只有当 Translated 变化时才触发
        if (e.PropertyName != nameof(TranslateItemModel.Translated)) return;
        // 更新父级的 Check, LineCount 等
        UpdateMetrics();
        if (CharacterTranslateChangedEnabled) CharacterTranslateChanged?.Invoke(this, EventArgs.Empty);
    }


    public event EventHandler? ContentTranslateChanged;
    public event EventHandler? CharacterTranslateChanged;

    private void UpdateMetrics()
    {
        var formatted = _isFormatting ? Content.Translated : FormatContent(Content.Translated);
        if (formatted != Content.Translated)
        {
            _isFormatting = true;
            try { Content.Translated = formatted; }
            finally { _isFormatting = false; }
            return;
        }

        // 这里的逻辑就是你原本在父级写的逻辑
        Check = CheckContent(Content.Translated);
        MaxLineLength = (Content.Translated + "\n").MaxLineLength();
        var rules = TranslationRules.Current;
        TooLong = rules.CheckLineLength && MaxLineLength >
            (Content.Original.LineCount() == 3 ? rules.ThreeLineMaxLength : rules.MaxLineLength);
    }
}

public partial class LineDialogModel
{
    public override string Result =>
        $"{Character.Result.Trim()}：{TranslationText.Normalize(Content.Result).Replace("\n", "\\N")}" + (EndLine ? "\n" : "");

    private int CharacterId { get; }

    public string Icon => CharacterId is > 0 and <= 31
        ? $"pack://application:,,,/Resource/Characters/chr_{CharacterId}.png"
        : string.Empty;

    private static string FormatContent(string content)
    {
        return TranslationRules.Current.Format(content);
    }

    private static string CheckContent(string content)
    {
        return TranslationRules.Current.Check(content);
    }
}
