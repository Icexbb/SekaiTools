using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.View.Setting;
using TextCopy;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Input;
using Button = System.Windows.Controls.Button;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateFastCopy : UserControl
{
    public TranslateFastCopy()
    {
        InitializeComponent();
        LoadCustomButtons();
    }

    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;


    private void AddCustomButton(string content)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = content,
            Margin = new Thickness(5),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ContextMenu = new ContextMenu
            {
                Items =
                {
                    new MenuItem
                    {
                        Header = "删除",
                        Command = new RelayCommand<Type>(t =>
                        {
                            var setting = SettingPageModel.Instance;
                            setting.CustomSpecialCharacters.Remove(content);
                            setting.SaveSetting();
                            LoadCustomButtons();
                        })
                    }
                }
            }
        };
        button.Click += ButtonBase_OnClick;
        CustomSpecialCharacters.Children.Add(button);
    }

    private void LoadCustomButtons()
    {
        CustomSpecialCharacters.Children.Clear();
        foreach (var character in SettingPageModel.Instance.CustomSpecialCharacters) AddCustomButton(character);
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button) return;
            ClipboardService.SetText(button.Content.ToString()!);
            SnackService.Show("成功", $"已复制 {button.Content} 到剪贴板", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.TextGrammarCheckmark24), new TimeSpan(0, 0, 2));
        }
        catch (Exception)
        {
            SnackService.Show("错误", "写入剪贴板失败", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.TextGrammarDismiss24), new TimeSpan(0, 0, 2));
            if (Debugger.IsAttached) throw;
        }
    }

    private async void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new AddCustomDialog(dialogService.GetDialogHost() ?? throw new InvalidOperationException());
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var element = dialog.ViewModel.CustomCharacter;

        var setting = SettingPageModel.Instance;
        if (setting.CustomSpecialCharacters.Contains(element)) return;
        setting.CustomSpecialCharacters.Add(element);
        setting.SaveSetting();
        LoadCustomButtons();
    }
}