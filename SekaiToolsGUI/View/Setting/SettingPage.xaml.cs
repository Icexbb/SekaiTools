using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.Setting.Components;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public partial class SettingPage : UserControl, IAppPage<SettingPageModel>
{
    public SettingPage()
    {
        DataContext = MainWindowViewModel.SettingPageModel;
        InitializeComponent();
    }

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    private async void ChooseDialogFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.DialogFontFamily = font;
    }

    private async void ChooseBannerFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.BannerFontFamily = font;
    }

    private async void ChooseMarkerFont(object sender, RoutedEventArgs e)
    {
        var font = await OpenFontDialog();
        if (font != "") ViewModel.MarkerFontFamily = font;
    }

    private async Task<string> OpenFontDialog()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new FontSelectDialog(ViewModel.DialogFontFamily);
        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        return dialogResult != ContentDialogResult.Primary ? "" : dialog.FontName;
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetSetting();
        SnackService.Show("成功", "设置已重置", ControlAppearance.Caution,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSetting();
        SnackService.Show("成功", "设置已保存", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
    }
}