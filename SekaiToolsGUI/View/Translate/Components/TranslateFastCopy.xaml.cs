using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.View.Setting;
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
                            var setting = new SettingPageModel();
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
        var setting = new SettingPageModel();
        foreach (var character in setting.CustomSpecialCharacters) AddCustomButton(character);
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button) Clipboard.SetText(button.Content.ToString()!);
    }

    private async void ButtonAdd_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new AddCustomDialog(dialogService.GetDialogHost() ?? throw new InvalidOperationException());
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var element = dialog.ViewModel.CustomCharacter;

        var setting = new SettingPageModel();
        if (setting.CustomSpecialCharacters.Contains(element)) return;
        setting.CustomSpecialCharacters.Add(element);

        setting.SaveSetting();
        LoadCustomButtons();
    }
}