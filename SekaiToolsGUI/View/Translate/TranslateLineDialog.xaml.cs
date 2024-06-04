using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;
using SekaiDialog = SekaiToolsCore.Story.Event.Dialog;

namespace SekaiToolsGUI.View.Translate;

public class LineDialogModel : ViewModelBase
{
    private SekaiDialog Dialog { get; }

    public LineDialogModel(SekaiDialog dialog)
    {
        Dialog = dialog;
        OriginalCharacter = dialog.CharacterOriginal;
        OriginalContent = dialog.BodyOriginal;
        if (dialog.CharacterTranslated != string.Empty) TranslatedCharacter = dialog.CharacterTranslated;
        TranslatedContent = dialog.BodyTranslated;
    }

    public SekaiDialog Export()
    {
        var dialog = new SekaiDialog(
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
            {
                lineRes += "【破折号使用错误】";
            }

            if (lineRes != "")
            {
                result += $"行{i+1}:{lineRes}\n";
            }
        }

        return result.Trim();
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
}

public partial class TranslateLineDialog : UserControl, INavigableView<LineDialogModel>, IExportable
{
    public LineDialogModel ViewModel => (LineDialogModel)DataContext;

    public TranslateLineDialog(SekaiDialog dialog)
    {
        DataContext = new LineDialogModel(dialog);
        InitializeComponent();
    }

    private void TranslatedContentTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((TranslatedContentTextBox.Text + "\n").LineCount() == 3)
                e.Handled = true;
        }
    }

    public string Export()
    {
        return (ViewModel.TranslatedCharacter == ""
                   ? ViewModel.OriginalCharacter
                   : ViewModel.TranslatedCharacter)
               + "："
               + (ViewModel.TranslatedContent == ""
                   ? ViewModel.OriginalContent
                   : ViewModel.TranslatedContent).Replace("\n", "\\N");
    }
}