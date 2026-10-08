using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTaskSetup : UserControl
{
    public SubtitleTaskSetup()
    {
        InitializeComponent();
    }

    private SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void SelectSameNameFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();

        string[] videoExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        string[] jsonExt = [".json", ".asset"];
        string[] txtExt = [".txt"];

        if (videoExt.Contains(fileExt))
        {
            ViewModel.VideoFilePath = filename;

            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (scriptPath == null && translatePath == null) return;

            var selectRelatedFiles = ViewModel.AutoSelectSameNameFiles;
            if (!selectRelatedFiles) return;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (jsonExt.Contains(fileExt))
        {
            ViewModel.ScriptFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);

            if (videoPath == null && translatePath == null) return;

            var selectRelatedFiles = ViewModel.AutoSelectSameNameFiles;
            if (!selectRelatedFiles) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (txtExt.Contains(fileExt))
        {
            ViewModel.TranslateFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (videoPath == null && scriptPath == null) return;

            var selectRelatedFiles = ViewModel.AutoSelectSameNameFiles;
            if (!selectRelatedFiles) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
        }

        return;

    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        SelectSameNameFile(result);
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result == null) return;

        SelectSameNameFile(result);
    }

    private void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result == null) return;

        SelectSameNameFile(result);
    }

    private void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
        var fileName = files[0];
        if (!File.Exists(fileName)) return;

        GetSameBaseFile(fileName);
    }

    private void GetSameBaseFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();
        List<string> vExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        List<string> sExt = [".json", ".asset"];
        List<string> tExt = [".txt"];
        if (vExt.Contains(fileExt) || sExt.Contains(fileExt) || tExt.Contains(fileExt))
            SelectSameNameFile(filename);
        else
            SnackService.Show("错误", "文件格式不支持", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentError24),
                new TimeSpan(0, 0, 3));
    }

    private void UIElement_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Link
            : DragDropEffects.None;
    }
}
