using Avalonia.Controls;
using Avalonia.Media;

namespace SekaiToolsAvalonia.View.Setting.Components;

public partial class FontSelectDialog : UserControl
{
    private TaskCompletionSource<string?> _tcs = new();

    public FontSelectDialog(string currentFont)
    {
        InitializeComponent();

        var fonts = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        FontListBox.ItemsSource = fonts;
        FontNameBox.Text = currentFont;

        FontListBox.SelectionChanged += (_, _) =>
        {
            if (FontListBox.SelectedItem is string font)
            {
                FontNameBox.Text = font;
                _tcs.TrySetResult(font);
            }
        };

        FontNameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
                _tcs.TrySetResult(FontNameBox.Text);
        };
    }

    public async Task<string?> ShowDialog(Window? owner)
    {
        var window = new Window
        {
            Title = "选择字体",
            Content = this,
            Width = 400,
            Height = 500,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        };

        window.Closed += (_, _) => _tcs.TrySetResult(null);
        await window.ShowDialog(owner);
        return await _tcs.Task;
    }
}
