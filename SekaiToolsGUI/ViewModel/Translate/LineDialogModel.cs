using System.ComponentModel;
using System.Globalization;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Story;
using SekaiToolsBase.Utils;
using SekaiToolsBase.Story.Translation;
using SekaiToolsConfiguration;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class LineDialogModel : LineModel
{
    private bool _isFormatting;
    public bool AutoFormat { get; set; } = true;
    public bool DeferFormatting { get; set; }
    public bool RestoringText { get; set; }
    public string InputHint { get => GetProperty(string.Empty); private set => SetProperty(value); }

    public void CompleteTextEdit()
    {
        DeferFormatting = false;
        UpdateMetrics();
    }

    public bool CanReplaceText(string text, int start, int length, string replacement)
    {
        var candidate = text.Remove(start, length).Insert(start, replacement);
        static int Lines(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n').Count(c => c == '\n') + 1;
        var previousLines = Lines(text);
        var nextLines = Lines(candidate);
        var allowed = nextLines <= 3 || nextLines <= previousLines;
        InputHint = allowed ? string.Empty : "正文最多三行。请先删减换行，再输入或粘贴；自动折行不占正文行数。";
        return allowed;
    }
    public LineDialogModel(DialogStoryEvent dialogStoryEvent, SekaiToolsBase.Story.FlashbackInfo? flashback = null, bool autoFormat = true)
    {
        AutoFormat = autoFormat;
        Flashback = flashback;
        EndLine = dialogStoryEvent.CloseWindow;
        Shake = dialogStoryEvent.Shake;

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

    public bool Shake { get; }
    public SekaiToolsBase.Story.FlashbackInfo? Flashback { get; }
    public bool IsFlashback => Flashback?.IsFlashback == true;
    public string FlashbackHint => IsFlashback ? Flashback!.Hint : string.Empty;
    public string FlashbackLabel
    {
        get => GetProperty(IsFlashback ? "疑似闪回 · " + string.Join("；", Flashback!.Sources
            .Where(source => source != Flashback.MainSource).Select(FlashbackAnalyzer.DescribeSource)) : string.Empty);
        private set => SetProperty(value);
    }

    public void ApplyFlashbackOrigins(IReadOnlyDictionary<string, SekaiToolsBase.Story.FlashbackEventOrigin> origins)
    {
        if (!IsFlashback) return;
        FlashbackLabel = "疑似闪回 · " + string.Join("；", Flashback!.Sources.Where(source => source != Flashback.MainSource)
            .Select(source => origins.TryGetValue(source, out var origin) ? origin.DisplayName : FlashbackAnalyzer.DescribeSource(source)));
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
        if (e.PropertyName is not (nameof(TranslateItemModel.Translated) or nameof(TranslateItemModel.PreserveEmpty))) return;
        // 更新父级的 Check, LineCount 等
        UpdateMetrics();
        NotifyContentState();
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
        var formatted = _isFormatting || RestoringText || DeferFormatting || !AutoFormat
            ? Content.Translated : FormatContent(Content.Translated);
        if (formatted != Content.Translated)
        {
            _isFormatting = true;
            try { Content.Translated = formatted; }
            finally { _isFormatting = false; }
            return;
        }

        MaxLineLength = (Content.Translated + "\n").MaxLineLength();
        var results = TranslationRules.Current.Evaluate(Content.Translated, new Dictionary<string, string>
        {
            ["originalLineCount"] = Content.Original.LineCount().ToString(CultureInfo.InvariantCulture),
            ["maxLineLength"] = MaxLineLength.ToString(CultureInfo.InvariantCulture)
        });
        Check = TranslationRules.FormatResults(results);
        if (Content.Translated.Count(c => c == '\n') >= 3)
            Check = string.Join("\n", new[] { Check, "正文超过三行，请删减换行。" }.Where(text => text.Length > 0));
        TooLong = results.Any(result => result.Feedback.Tags.Contains("length"));
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

}
