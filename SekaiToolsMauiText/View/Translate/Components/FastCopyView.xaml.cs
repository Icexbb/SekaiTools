using System.Text.Json;

namespace SekaiToolsMauiText.View.Translate.Components;

public partial class FastCopyView : ContentView
{
    private const string CustomCharsKey = "FastCopy_CustomChars";

    public FastCopyView()
    {
        InitializeComponent();
        LoadCustomButtons();
    }

    private static List<string> LoadCustomChars()
    {
        var json = Preferences.Get(CustomCharsKey, "[]");
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }

    private static void SaveCustomChars(List<string> chars)
    {
        Preferences.Set(CustomCharsKey, JsonSerializer.Serialize(chars));
    }

    private void LoadCustomButtons()
    {
        CustomCharsPanel.Children.Clear();
        foreach (var ch in LoadCustomChars())
            AddCustomButton(ch);
    }

    private void AddCustomButton(string content)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };

        var btn = new Button { Text = content, HorizontalOptions = LayoutOptions.Fill, Margin = new Thickness(0, 0, 2, 0) };
        btn.Clicked += OnSpecialCharClicked;
        Grid.SetColumn(btn, 0);

        var del = new Button { Text = "✕", WidthRequest = 36, FontSize = 11, BackgroundColor = Colors.Transparent };
        del.Clicked += (_, _) =>
        {
            var chars = LoadCustomChars();
            chars.Remove(content);
            SaveCustomChars(chars);
            LoadCustomButtons();
        };
        Grid.SetColumn(del, 1);

        grid.Children.Add(btn);
        grid.Children.Add(del);
        CustomCharsPanel.Children.Add(grid);
    }

    private async void OnSpecialCharClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;
        try
        {
            await Clipboard.SetTextAsync(btn.Text);
        }
        catch
        {
            // Clipboard may not be available in all environments
        }
    }

    private async void OnAddCustomClicked(object? sender, EventArgs e)
    {
        var page = GetParentPage();
        if (page == null) return;

        var input = await page.DisplayPromptAsync("添加自定义字符", "请输入要快速复制的字符：", "添加", "取消");
        if (string.IsNullOrEmpty(input)) return;

        var chars = LoadCustomChars();
        if (chars.Contains(input)) return;
        chars.Add(input);
        SaveCustomChars(chars);
        LoadCustomButtons();
    }

    private Page? GetParentPage()
    {
        Element? current = this;
        while (current != null)
        {
            if (current is Page page) return page;
            current = current.Parent;
        }

        return null;
    }
}

