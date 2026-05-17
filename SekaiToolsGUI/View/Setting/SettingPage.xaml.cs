using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.Setting.Components;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public partial class SettingPage : UserControl, IAppPage<SettingPageModel>
{
    private readonly ICollectionView _logView;

    public SettingPage()
    {
        _logView = CollectionViewSource.GetDefaultView(InMemoryLogSink.Entries);
        _logView.Filter = _ => true;

        DataContext = MainWindowViewModel.SettingPageModel;
        InitializeComponent();

        BindingOperations.EnableCollectionSynchronization(InMemoryLogSink.Entries, InMemoryLogSink.Entries);
        LogListBox.ItemsSource = _logView;

        ((INotifyCollectionChanged)LogListBox.Items).CollectionChanged += (_, _) =>
        {
            if (LogListBox.Items.Count > 0)
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        };
    }

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SettingPageModel ViewModel => (SettingPageModel)DataContext;

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        InMemoryLogSink.Clear();
    }

    private static bool FilterAll(object obj) => true;

    private static bool FilterInfoPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Information;

    private static bool FilterWarnPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Warning;

    private static bool FilterErrorPlus(object obj)
        => obj is LogEntry e && e.Level <= LogLevel.Error;

    private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _logView.Filter = LevelFilter.SelectedIndex switch
        {
            1 => FilterInfoPlus,
            2 => FilterWarnPlus,
            3 => FilterErrorPlus,
            _ => FilterAll
        };
    }

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
