using Avalonia.Controls;
using Avalonia.Interactivity;
using SekaiToolsAvalonia.ViewModel.Setting;

namespace SekaiToolsAvalonia.View.Translate.Components;

public partial class TranslateFastCopy : UserControl
{
    public TranslateFastCopy()
    {
        InitializeComponent();
        LoadCustomButtons();
    }

    private void AddCustomButton(string content)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Avalonia.Thickness(2),
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        button.Click += ButtonBase_OnClick;
        var deleteItem = new MenuItem { Header = "删除" };
        deleteItem.Click += (_, _) =>
        {
            var setting = SettingPageModel.Instance;
            setting.CustomSpecialCharacters.Remove(content);
            setting.SaveSetting();
            LoadCustomButtons();
        };
        button.ContextMenu = new ContextMenu();
        button.ContextMenu.Items.Add(deleteItem);
        CustomSpecialCharacters.Children.Add(button);
    }

    private void LoadCustomButtons()
    {
        CustomSpecialCharacters.Children.Clear();
        foreach (var character in SettingPageModel.Instance.CustomSpecialCharacters)
            AddCustomButton(character);
    }

    private async void ButtonBase_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var text = button.Content?.ToString() ?? "";
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard != null)
            await topLevel.Clipboard.SetTextAsync(text);
    }

    private async void ButtonAdd_OnClick(object? sender, RoutedEventArgs e)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var window = new Window
        {
            Title = "添加自定义字符", Width = 300, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var textBox = new TextBox { Margin = new Avalonia.Thickness(10) };
        var addBtn = new Button { Content = "添加", Margin = new Avalonia.Thickness(10) };
        window.Content = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "输入自定义字符:", Margin = new Avalonia.Thickness(10, 10, 10, 0) },
                textBox, addBtn
            }
        };

        var tcs = new TaskCompletionSource<bool>();
        addBtn.Click += (_, _) => { tcs.TrySetResult(true); window.Close(); };
        window.Closing += (_, _) => tcs.TrySetResult(false);
        await window.ShowDialog(owner);

        if (await tcs.Task && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var setting = SettingPageModel.Instance;
            if (!setting.CustomSpecialCharacters.Contains(textBox.Text))
            {
                setting.CustomSpecialCharacters.Add(textBox.Text);
                setting.SaveSetting();
                LoadCustomButtons();
            }
        }
    }
}
