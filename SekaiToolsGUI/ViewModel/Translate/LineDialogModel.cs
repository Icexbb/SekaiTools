using System.ComponentModel;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;

namespace SekaiToolsGUI.ViewModel.Translate;

public partial class LineDialogModel : LineModel
{
    public LineDialogModel(DialogStoryEvent dialogStoryEvent)
    {
        EndLine = dialogStoryEvent.CloseWindow;

        CharacterId = dialogStoryEvent.CharacterId;

        Character.Original = dialogStoryEvent.CharacterOriginal;
        Character.Translated = string.IsNullOrWhiteSpace(dialogStoryEvent.CharacterTranslated)
            ? dialogStoryEvent.CharacterOriginal
            : dialogStoryEvent.CharacterTranslated;


        Content.Original = dialogStoryEvent.BodyOriginal;
        Content.Translated = dialogStoryEvent.BodyTranslated;

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
        var formatted = FormatContent(Content.Translated);
        if (formatted != Content.Translated)
        {
            Content.Translated = formatted;
            return;
        }

        // 这里的逻辑就是你原本在父级写的逻辑
        Check = CheckContent(Content.Translated);
        MaxLineLength = (Content.Translated + "\n").MaxLineLength();
        TooLong = Content.Original.LineCount() == 3 ? MaxLineLength > 45 : MaxLineLength > 37;
    }
}

public partial class LineDialogModel
{
    public override string Result =>
        $"{Character.Result}：{Content.Result.Replace("\n", "\\N")}" + (EndLine ? "\n" : "");

    private int CharacterId { get; }

    public string Icon => CharacterId is > 0 and <= 31
        ? $"pack://application:,,,/Resource/Characters/chr_{CharacterId}.png"
        : string.Empty;

    private static string FormatContent(string content)
    {
        return content.Replace("…", "...")
            .Replace('(', '（')
            .Replace(')', '）')
            .Replace(',', '，')
            .Replace('?', '？')
            .Replace('!', '！')
            .Replace('欸', '诶');
    }

    private static string CheckContent(string content)
    {
        var result = "";
        var normalEnds = new[] { '、', '，', '。', '？', '！', '~', '♪', '☆', '.', '—' };
        var abnormalEnds = new[] { '）', '」', '』', '”' };
        var contentArray = content.Split("\n").Where(s => s.Length > 0).ToList();
        for (var i = 0; i < contentArray.Count; i++)
        {
            var line = contentArray[i];
            var lineRes = "";
            var last = line.Last();
            if (normalEnds.Contains(last) || abnormalEnds.Contains(last))
            {
                if (normalEnds.Contains(last) && (line.EndsWith(".，") || line.EndsWith(".。")))
                    lineRes += "【「……。」和「……，」只保留省略号】";
                else if (line.Length > 1 && abnormalEnds.Contains(line[^2]))
                    lineRes += "【句尾缺少逗号句号】";
            }
            else
            {
                lineRes += "【句尾缺少逗号句号】";
            }

            if (line.Contains('—') && line.Contains("——") &&
                line.Split("—").Length != line.Split("——").Length * 2 - 1)
                lineRes += "【破折号使用错误】";

            if (lineRes != "") result += $"行{i + 1}:{lineRes}\n";
        }

        return result.Trim();
    }
}