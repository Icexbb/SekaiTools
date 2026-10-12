using System.IO;
using System.Diagnostics;
using SekaiToolsBase;
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

    private int _selectionVersion;

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var timer = Stopwatch.StartNew();
        var result = openFileDialog.ShowDialog();
        Logger.Log($"文件对话框（含用户选择时间）: {timer.Elapsed.TotalMilliseconds:F0} ms; 已选择={result == true}");
        return result == true ? openFileDialog.FileName : null;
    }

    private async Task SelectSameNameFileAsync(string filename)
    {
        var version = ++_selectionVersion;
        var model = ViewModel;
        var fileExt = Path.GetExtension(filename).ToLowerInvariant();
        string[] videoExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        string[] scriptExt = [".json", ".asset"];
        if (videoExt.Contains(fileExt)) model.VideoFilePath = filename;
        else if (scriptExt.Contains(fileExt)) model.ScriptFilePath = filename;
        else if (fileExt == ".txt") model.TranslateFilePath = filename;
        else return;
        if (!model.AutoSelectSameNameFiles) return;

        var timer = Stopwatch.StartNew();
        try
        {
            // File-system queries can block on a slow disk. Keep them off the dispatcher.
            var related = await Task.Run(() => (
                Video: videoExt.Contains(fileExt) ? null : videoExt.Select(ext => Path.ChangeExtension(filename, ext)).FirstOrDefault(File.Exists),
                Script: scriptExt.Contains(fileExt) ? null : scriptExt.Select(ext => Path.ChangeExtension(filename, ext)).FirstOrDefault(File.Exists),
                Translation: fileExt == ".txt" ? null : new[] { Path.ChangeExtension(filename, ".txt") }.FirstOrDefault(File.Exists)));
            // A later selection, dialog close or checkbox change invalidates this result.
            if (version != _selectionVersion || !IsLoaded || DataContext != model || !model.AutoSelectSameNameFiles) return;
            if (related.Video != null) model.VideoFilePath = related.Video;
            if (related.Script != null) model.ScriptFilePath = related.Script;
            if (related.Translation != null) model.TranslateFilePath = related.Translation;
        }
        catch (Exception exception)
        {
            Logger.Log($"匹配配套文件失败: {exception}");
        }
        finally
        {
            Logger.Log($"匹配配套文件: {timer.Elapsed.TotalMilliseconds:F0} ms");
        }
    }

    private async void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        await SelectSameNameFileAsync(result);
    }

    private async void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result == null) return;

        await SelectSameNameFileAsync(result);
    }

    private async void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result == null) return;

        await SelectSameNameFileAsync(result);
    }

    private async void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files) return;
        var fileName = files[0];
        if (!File.Exists(fileName)) return;

        await GetSameBaseFileAsync(fileName);
    }

    private async Task GetSameBaseFileAsync(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();
        List<string> vExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        List<string> sExt = [".json", ".asset"];
        List<string> tExt = [".txt"];
        if (vExt.Contains(fileExt) || sExt.Contains(fileExt) || tExt.Contains(fileExt))
            await SelectSameNameFileAsync(filename);
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
