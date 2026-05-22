using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.ViewModel.Suppress;

namespace SekaiToolsAvalonia.View.Suppress;

public partial class SuppressPage : UserControl, IAppPage
{
    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();
    }

    public void OnNavigatedTo() { }

    private SuppressPageModel ViewModel => (SuppressPageModel)DataContext!;

    private async void VideoFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await SelectFileAsync("选择视频文件", ["mp4", "avi", "mkv", "webm", "wmv"]);
        if (file != null) ViewModel.SourceVideo = file;
    }

    private async void ScriptFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var file = await SelectFileAsync("选择字幕文件", ["ass"]);
        if (file != null) ViewModel.SourceSubtitle = file;
    }

    private async void SaveFileBrowser_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择输出路径",
            SuggestedFileName = Path.GetFileName(ViewModel.OutputPath),
            FileTypeChoices = [new FilePickerFileType("MP4文件") { Patterns = ["*.mp4"] }]
        });
        if (file != null) ViewModel.OutputPath = file.Path.LocalPath;
    }

    private async Task<string?> SelectFileAsync(string title, string[] exts)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;
        var types = exts.Select(e => new FilePickerFileType(e) { Patterns = [$"*.{e}"] }).ToList();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title, AllowMultiple = false, FileTypeFilter = types
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private void StartSuppress_OnClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Wire up VapourSynth + FFmpeg encoding pipeline
    }

    private void DisposeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Running = false;
    }

    private void ClearButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Reset();
    }

    private void ShowFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (File.Exists(ViewModel.OutputPath))
            Process.Start(new ProcessStartInfo("Explorer.exe")
                { Arguments = "/e,/select," + ViewModel.OutputPath, UseShellExecute = true });
    }
}
