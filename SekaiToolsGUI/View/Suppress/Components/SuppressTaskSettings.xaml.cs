using System.IO;
using System.Windows;
using Microsoft.Win32;
using SekaiToolsGUI.ViewModel.Suppress;

namespace SekaiToolsGUI.View.Suppress.Components;

public partial class SuppressTaskSettings
{
    public SuppressTaskSettings()
    {
        DataContext = new SuppressTaskSettingsModel();
        InitializeComponent();
    }

    public SuppressTaskSettingsModel ViewModel => (SuppressTaskSettingsModel)DataContext;

    private static string? SelectFile(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile("视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result != null) ViewModel.SourceVideo = result;
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile("字幕文件|*.ass");
        if (result != null) ViewModel.SourceSubtitle = result;
    }

    private void ClearSubtitle_OnClick(object sender, RoutedEventArgs e) => ViewModel.SourceSubtitle = "";

    private void SaveFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Mp4 文件|*.mp4",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.SourceVideo),
            DefaultExt = ".mp4",
            FileName = Path.ChangeExtension("[STVS]" + Path.GetFileName(ViewModel.SourceVideo), ".mp4")
        };
        if (dialog.ShowDialog() == true) ViewModel.OutputPath = dialog.FileName;
    }
}
