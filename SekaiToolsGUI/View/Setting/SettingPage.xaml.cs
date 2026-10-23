using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.Setting.Components;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace SekaiToolsGUI.View.Setting;

public partial class SettingPage : UserControl, IAppPage<SettingPageModel>
{
    public SettingPage() : this(MainWindowViewModel.SettingPageModel)
    {
    }

    internal SettingPage(SettingPageModel model)
    {
        DataContext = model;
        InitializeComponent();
        var specialSections = new Dictionary<string, FrameworkElement> { ["about"] = AboutSection };
        SettingsItems.Children.Clear();
        var builder = new SettingViewBuilder(OpenFontDialog, key => specialSections[key]);
        foreach (var section in SettingCatalog.Load()) SettingsItems.Children.Add(builder.Build(section));
    }

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private void OpenLogs_Click(object sender, RoutedEventArgs e) => RuntimeLogWindow.Open(Window.GetWindow(this));

    private async Task<string> OpenFontDialog(string currentFontFamily)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new FontSelectDialog(currentFontFamily);
        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        return dialogResult != ContentDialogResult.Primary ? "" : dialog.FontName;
    }

    private async void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "恢复默认设置？",
            Content = "主题、代理、字体和字幕输出设置都将恢复为默认值。",
            PrimaryButtonText = "恢复默认",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        ViewModel.ResetSetting();
        SnackService.Show("成功", "设置已重置", ControlAppearance.Caution,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
    }
}
