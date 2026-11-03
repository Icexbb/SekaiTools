using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.Control;
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
        var specialSections = new Dictionary<string, FrameworkElement>
        {
            ["about"] = AboutSection,
            ["subtitle-export"] = new ExportOptionsControl
            {
                ShowResetButtons = true,
                Description = "选择字幕输出的类别",
                IsExpanded = false
            }
        };
        SettingsItems.Children.Clear();
        var builder = new SettingViewBuilder(OpenFontDialog, key => specialSections[key]);
        var sections = SettingCatalog.Load();
        foreach (var category in SettingCatalog.Categories)
        {
            var content = new StackPanel();
            foreach (var section in sections.Where(section => section.Category == category && section.Custom is not ("logs" or "about")))
                content.Children.Add(section.Custom == null && section.Properties.Contains("ExportLine1")
                    ? new ExportOptionsControl
                    {
                        ShowResetButtons = true,
                        Description = section.Description,
                        IsExpanded = section.IsExpanded
                    }
                    : builder.Build(section));
            if (content.Children.Count == 0) continue;
            var icon = category switch
            {
                "轴机" => SymbolRegular.Subtitles24,
                "下载" => SymbolRegular.ArrowDownload24,
                "翻译" => SymbolRegular.Translate24,
                "压制" => SymbolRegular.AnimalTurtle24,
                _ => SymbolRegular.Settings24
            };
            var group = new CardExpander
            {
                Header = category,
                Icon = new SymbolIcon(icon),
                Content = content,
                IsExpanded = true,
                Margin = new Thickness(0, 0, 0, 12),
                ContentPadding = new Thickness(8)
            };
            System.Windows.Automation.AutomationProperties.SetName(group, $"{category}设置");
            SettingsItems.Children.Add(group);
        }
        SettingsItems.Children.Add(AboutSection);
    }

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        RuntimeLogWindow.Open(Window.GetWindow(this));
    }

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
            Content = "所有模块的全局设置都将恢复为默认值。",
            PrimaryButtonText = "恢复默认",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        if (result != ContentDialogResult.Primary) return;

        ViewModel.ResetSetting();
        SnackService.Show("成功", "设置已重置", ControlAppearance.Caution,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
    }
}