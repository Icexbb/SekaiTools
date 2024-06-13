using System.IO;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate;

public class SaveFileDialogModel : ViewModelBase
{
    public string FileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}

public partial class SaveFileDialog : ContentDialog
{
    public SaveFileDialogModel ViewModel => (SaveFileDialogModel)DataContext;
    private string ScriptFile { get; }
    private string TranslationFile { get; }

    public SaveFileDialog(ContentPresenter contentPresenter, string scriptFile, string translationFile = "") : base(
        contentPresenter)
    {
        ScriptFile = scriptFile;
        TranslationFile = translationFile;
        DataContext = new SaveFileDialogModel();
        ViewModel.FileName = TranslationFile == ""
            ? Path.ChangeExtension(ScriptFile, ".txt")
            : TranslationFile;
        InitializeComponent();
    }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        switch (button)
        {
            case ContentDialogButton.Primary:
                base.OnButtonClick(button);
                break;
            case ContentDialogButton.Secondary:
                ViewModel.FileName = SelectFile() ?? ViewModel.FileName;
                break;
            case ContentDialogButton.Close:
                base.OnButtonClick(button);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(button), button, null);
        }
    }

    private string? SelectFile()
    {
        var openFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "文本文件|*.txt;",
            DefaultDirectory = Path.GetDirectoryName(TranslationFile == "" ? ScriptFile : TranslationFile),
            DefaultExt = ".txt",
            FileName = TranslationFile == ""
                ? Path.ChangeExtension(Path.GetFileName(ScriptFile), ".txt")
                : Path.GetFileName(TranslationFile)
        };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }
}