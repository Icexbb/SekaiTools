using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View;

public partial class TaskSetting : UserControl
{
    public TaskSetting()
    {
        InitializeComponent();
        DataContext = new TaskSettingModel();
    }

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result != null) (DataContext as TaskSettingModel)!.VideoFilePath = result;
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result != null) (DataContext as TaskSettingModel)!.ScriptFilePath = result;
    }

    private void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result != null) (DataContext as TaskSettingModel)!.TranslateFilePath = result;
    }
}