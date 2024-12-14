using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.View.Setting.Components;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public partial class SettingPage : UserControl, INavigableView<SettingPageModel>
{
    public SettingPage()
    {
        DataContext = MainWindowViewModel.SettingPageModel;
        InitializeComponent();
    }

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;


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
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        return dialogResult != ContentDialogResult.Primary ? "" : dialog.FontName;
    }
}