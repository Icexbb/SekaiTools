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

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".avi", ".mkv", ".webm", ".wmv" };
    private static bool AcceptFiles(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is not string[] files || files.Length is < 1 or > 2) return false;
        return files.All(File.Exists) && files.Count(x => VideoExtensions.Contains(Path.GetExtension(x))) <= 1
            && files.Count(x => Path.GetExtension(x).Equals(".ass", StringComparison.OrdinalIgnoreCase)) <= 1
            && files.All(x => VideoExtensions.Contains(Path.GetExtension(x)) || Path.GetExtension(x).Equals(".ass", StringComparison.OrdinalIgnoreCase));
    }
    private void Files_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = AcceptFiles(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }
    private void Files_OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!AcceptFiles(e.Data)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var video = files.FirstOrDefault(x => VideoExtensions.Contains(Path.GetExtension(x)));
        if (video != null) ViewModel.SourceVideo = video;
        var subtitle = files.FirstOrDefault(x => Path.GetExtension(x).Equals(".ass", StringComparison.OrdinalIgnoreCase));
        if (subtitle != null) { ViewModel.SourceSubtitle = subtitle; ViewModel.SubtitleNotice = "已使用拖入的 ASS 字幕"; }
    }

    private void RetryAnalysis_OnClick(object sender, RoutedEventArgs e) => ViewModel.RetryAnalysis();

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
        if (result != null) { ViewModel.SourceSubtitle = result; ViewModel.SubtitleNotice = "已手动选择 ASS 字幕"; }
    }

    private void ClearSubtitle_OnClick(object sender, RoutedEventArgs e) { ViewModel.SourceSubtitle = ""; ViewModel.SubtitleNotice = "未选择字幕，将仅转码"; }

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
