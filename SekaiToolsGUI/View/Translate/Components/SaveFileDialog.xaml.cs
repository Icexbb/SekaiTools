using System.IO;
using System.Windows;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class SaveFileDialog : ContentDialog
{
    public SaveFileDialog(ContentDialogHost contentPresenter, string scriptFile, string translationFile = "") : base(
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

    public SaveFileDialogModel ViewModel => (SaveFileDialogModel)DataContext;
    public bool UseV2 => FormatSelector.SelectedIndex == 0;
    private string ScriptFile { get; }
    private string TranslationFile { get; }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        switch (button)
        {
            case ContentDialogButton.Primary:
                base.OnButtonClick(button);
                break;
            case ContentDialogButton.Secondary:
                SelectFile();
                break;
            case ContentDialogButton.Close:
                base.OnButtonClick(button);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(button), button, null);
        }
    }

    private void SelectFile()
    {
        var openFileDialog = new Microsoft.Win32.SaveFileDialog
        {
            OverwritePrompt = true,
            Filter = "文本文件|*.txt;",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.FileName),
            DefaultExt = ".txt",
            FileName = Path.GetFileName(ViewModel.FileName)
        };
        var existingFileConfirmed = false;
        openFileDialog.FileOk += (_, _) => existingFileConfirmed = File.Exists(openFileDialog.FileName);
        if (openFileDialog.ShowDialog() == true)
            ViewModel.SetSelectedPath(openFileDialog.FileName, existingFileConfirmed);
    }

    private void TranslationTag_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string tag })
            ViewModel.ApplyTranslationTag(tag);
    }
}
