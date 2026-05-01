using Microsoft.Maui.Graphics;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Utils;

namespace SekaiToolsMauiText.ViewModel;

public class LineDialogModel : LineModel
{
    private readonly DialogStoryEvent _storyEvent;

    public LineDialogModel(DialogStoryEvent dialogStoryEvent)
    {
        _storyEvent = dialogStoryEvent;
        EndLine = dialogStoryEvent.CloseWindow;
        OriginalCharacter = dialogStoryEvent.CharacterOriginal;
        TranslatedCharacter = string.IsNullOrWhiteSpace(dialogStoryEvent.CharacterTranslated)
            ? dialogStoryEvent.CharacterOriginal
            : dialogStoryEvent.CharacterTranslated;
        OriginalContent = dialogStoryEvent.BodyOriginal;
        TranslatedContent = dialogStoryEvent.BodyTranslated;
    }

    public string Icon => _storyEvent.CharacterId is > 0 and <= 31
        ? $"chr_{_storyEvent.CharacterId}.png"
        : string.Empty;

    public bool HasIcon => !string.IsNullOrEmpty(Icon);
    public bool HasNoIcon => string.IsNullOrEmpty(Icon);

    public bool EndLine
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string OriginalCharacter
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedCharacter
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            if (CharacterTranslateChangedEnabled)
                CharacterTranslateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string CharacterReference
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasCharacterReference));
        }
    }

    public bool HasCharacterReference => !string.IsNullOrEmpty(CharacterReference);

    public string OriginalContent
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }

    public string TranslatedContent
    {
        get => GetProperty(string.Empty);
        set
        {
            var v = FormatContent(value);
            Check = CheckContent(v);
            SetProperty(v);
            LineCount = (v + "\n").LineCount();
            MaxLineLength = (v + "\n").MaxLineLength();
            OnPropertyChanged(nameof(HasCheck));
            OnPropertyChanged(nameof(HasTranslatedContent));
            OnPropertyChanged(nameof(LengthColor));
            if (ContentTranslateChangedEnabled)
                ContentTranslateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ContentReference
    {
        get => GetProperty(string.Empty);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(HasContentReference));
        }
    }

    public bool HasContentReference => !string.IsNullOrEmpty(ContentReference);
    public bool HasTranslatedContent => !string.IsNullOrEmpty(TranslatedContent);
    public bool HasCheck => !string.IsNullOrEmpty(Check);

    public int LineCount
    {
        get => GetProperty(0);
        set => SetProperty(value);
    }

    public int MaxLineLength
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            TooLong = OriginalContent.LineCount() == 3 ? value > 45 : value > 37;
        }
    }

    public bool TooLong
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(LengthColor));
        }
    }

    public Color LengthColor => TooLong ? Colors.Red : Colors.Gray;

    public string Check
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public bool CharacterTranslateChangedEnabled { get; set; } = true;
    public bool ContentTranslateChangedEnabled { get; set; } = true;

    public event EventHandler? CharacterTranslateChanged;
    public event EventHandler? ContentTranslateChanged;

    public override string Result
    {
        get
        {
            var charResult = string.IsNullOrWhiteSpace(TranslatedCharacter) ? OriginalCharacter : TranslatedCharacter;
            var contentResult = string.IsNullOrWhiteSpace(TranslatedContent) ? OriginalContent : TranslatedContent;
            return $"{charResult}：{contentResult.Replace("\n", "\\N")}" + (EndLine ? "\n" : "");
        }
    }

    public DialogStoryEvent Export()
    {
        var dialog = new DialogStoryEvent(
            _storyEvent.Index, OriginalContent,
            _storyEvent.CharacterId, OriginalCharacter,
            _storyEvent.CloseWindow, _storyEvent.Shake);
        dialog.SetTranslation(TranslatedCharacter, TranslatedContent);
        return dialog;
    }

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