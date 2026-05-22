using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.ViewModel.Setting;

namespace SekaiToolsAvalonia.View.Setting;

public partial class SettingPage : UserControl, IAppPage
{
    private int _logLevel = 0;

    public SettingPage()
    {
        DataContext = SettingPageModel.Instance;
        InitializeComponent();
        RefreshLogView();
    }

    public void OnNavigatedTo() { }

    private SettingPageModel ViewModel => (SettingPageModel)DataContext!;

    private async void ChooseDialogFont(object? sender, RoutedEventArgs e)
    {
        var result = await ShowFontDialog(ViewModel.DialogFontFamily);
        if (!string.IsNullOrEmpty(result)) ViewModel.DialogFontFamily = result;
    }

    private async void ChooseBannerFont(object? sender, RoutedEventArgs e)
    {
        var result = await ShowFontDialog(ViewModel.BannerFontFamily);
        if (!string.IsNullOrEmpty(result)) ViewModel.BannerFontFamily = result;
    }

    private async void ChooseMarkerFont(object? sender, RoutedEventArgs e)
    {
        var result = await ShowFontDialog(ViewModel.MarkerFontFamily);
        if (!string.IsNullOrEmpty(result)) ViewModel.MarkerFontFamily = result;
    }

    private async Task<string?> ShowFontDialog(string currentFont)
    {
        var dialog = new Components.FontSelectDialog(currentFont);
        var owner = TopLevel.GetTopLevel(this) as Window;
        return await dialog.ShowDialog(owner);
    }

    private void ClearLog_Click(object? sender, RoutedEventArgs e)
    {
        InMemoryLogSink.Clear();
        RefreshLogView();
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e) => ViewModel.SaveSetting();
    private void ResetToDefault_Click(object? sender, RoutedEventArgs e) => ViewModel.ResetSetting();

    private void OpenGitHub_Click(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Icexbb/SekaiTools",
            UseShellExecute = true
        });
    }

    private void LevelFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LogListBox is null) return;
        _logLevel = LevelFilter.SelectedIndex;
        RefreshLogView();
    }

    private void RefreshLogView()
    {
        var entries = InMemoryLogSink.Entries.AsEnumerable();
        entries = _logLevel switch
        {
            1 => entries.Where(e => e.Level <= LogLevel.Information),
            2 => entries.Where(e => e.Level <= LogLevel.Warning),
            3 => entries.Where(e => e.Level <= LogLevel.Error),
            _ => entries
        };
        LogListBox.ItemsSource = entries.Select(e => e.ToString()).ToList();
    }
}
