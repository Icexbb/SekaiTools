using System.IO;
using System.Windows;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SaveFileDialog : ContentDialog
{
    public SaveFileDialog(ContentDialogHost contentPresenter, string videoFile) : base(contentPresenter)
    {
        VideoFile = videoFile;
        DataContext = new SaveFileDialogModel();
        ViewModel.FileName = Path.Join(
            Path.GetDirectoryName(VideoFile),
            "[STGenerated] " + Path.ChangeExtension(Path.GetFileName(VideoFile), ".ass")
        );
        InitializeComponent();
    }

    public SaveFileDialogModel ViewModel => (SaveFileDialogModel)DataContext;
    private string VideoFile { get; }

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
            Filter = "Advanced SubStation Alpha 字幕文件|*.ass;",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.FileName),
            DefaultExt = ".ass",
            FileName = Path.GetFileName(ViewModel.FileName)
        };
        var existingFileConfirmed = false;
        openFileDialog.FileOk += (_, _) => existingFileConfirmed = File.Exists(openFileDialog.FileName);
        if (openFileDialog.ShowDialog() == true)
            ViewModel.SetSelectedPath(openFileDialog.FileName, existingFileConfirmed);
    }

    private void ButtonSelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        SelectFile();
    }
}