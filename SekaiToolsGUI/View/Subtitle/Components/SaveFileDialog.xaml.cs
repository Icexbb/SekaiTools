using System.IO;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SaveFileDialog : ContentDialog
{
    public SaveFileDialog(ContentPresenter contentPresenter, string videoFile) : base(contentPresenter)
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
            Filter = "Advanced SubStation Alpha 字幕文件|*.ass;",
            DefaultDirectory = Path.GetDirectoryName(VideoFile),
            DefaultExt = ".ass",
            FileName = "[STGenerated] " + Path.ChangeExtension(
                Path.GetFileName(VideoFile), ".ass")
        };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void ButtonSelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.FileName = SelectFile() ?? ViewModel.FileName;
    }
}