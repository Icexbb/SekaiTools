using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsConfiguration;
using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.Download.Components;
using SekaiToolsGUI.View.Translate.Components;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Development;

public partial class WorkbenchShowcase : FluentWindow
{
    private readonly int _originalTheme;

    public WorkbenchShowcase()
    {
        _originalTheme = SettingPageModel.Instance.CurrentApplicationTheme;
        InitializeComponent();
        DataContext = SettingPageModel.Instance;
        var dialogue = new DialogStoryEvent(1, "一緒に、新しい曲を作ろう。", 1, "初音ミク", false, false);
        dialogue.SetTranslation("初音未来", "一起创作新的歌曲吧。");
        TranslationSample.Content = new TranslateLineDialog { LineDialogModel = new LineDialogModel(dialogue) { SequenceNumber = 1 } };
        var task = new DownloadTask("示例剧情 | 第1话", "http://127.0.0.1/showcase.asset", "组件样例", ApplicationPaths.Current.DataRoot) { UseWorkbenchTheme = true };
        task.ChangeStatus(SekaiDataFetch.DownloadStatus.Succeeded);
        task.SetCanRemove(false);
        TaskSample.Content = task;
        Closed += (_, _) => SettingPageModel.Instance.CurrentApplicationTheme = _originalTheme;
        WindowWorkArea.Attach(this);
    }

    private void Threshold_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (ConfirmExample is null || ValidationMessage is null) return;
        var valid = double.TryParse(ThresholdInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) && value is > 0 and <= 1;
        ConfirmExample.IsEnabled = valid;
        ValidationMessage.IsOpen = !valid;
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e) => ResultMessage.Text = "示例已确认，译文和选择仍保留。";

    private void LongTitle_OnChanged(object sender, RoutedEventArgs e)
    {
        if (PreviewHeader is null) return;
        const string sentence = "长标题压力验证：脚本翻译、字幕识别与压制队列的公共工作区，中文、日本語与 English 混排时应完整换行并保持操作可达。";
        PreviewHeader.Title = LongTitleSample.IsChecked == true ? $"Fluent Workbench · {sentence}{sentence}" : "Fluent Workbench";
    }
}
