using SekaiToolsCore;
using SekaiToolsCore.Story.Event;

namespace SekaiToolsGUI.ViewModel;

public class LineDialogModel : ViewModelBase
{
    public LineDialogModel(Dialog dialog)
    {
        Dialog = dialog;
        OriginalCharacter = dialog.CharacterOriginal;
        OriginalContent = dialog.BodyOriginal;
        if (dialog.CharacterTranslated != string.Empty) TranslatedCharacter = dialog.CharacterTranslated;
        TranslatedContent = dialog.BodyTranslated;
    }

    private Dialog Dialog { get; }

    public string Icon => Dialog.CharacterId is > 0 and <= 31
        ? $"pack://application:,,,/Resource/Characters/chr_{Dialog.CharacterId}.png"
        // ? ""
        : string.Empty;

    public string OriginalCharacter
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }


    public string TranslatedCharacter
    {
        get => GetProperty(string.Empty);
        set => SetProperty(value);
    }


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
        }
    }

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
        set => SetProperty(value);
    }

    public string Check
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public Dialog Export()
    {
        var dialog = new Dialog(
            Dialog.Index,
            OriginalContent,
            Dialog.CharacterId,
            OriginalCharacter,
            Dialog.CloseWindow,
            Dialog.Shake
        );
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