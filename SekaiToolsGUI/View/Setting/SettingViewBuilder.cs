using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SekaiToolsGUI.View.Setting.Components;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SekaiToolsGUI.View.Setting;

/// <summary>Generic editor construction; setting-specific choices live in settings-ui.json.</summary>
public sealed class SettingViewBuilder(Func<string, Task<string>> selectFont, Func<string, FrameworkElement> customContent)
{
    public FrameworkElement Build(SettingSection section, bool nested = false)
    {
        if (section.Custom is { } custom) return customContent(custom);
        var header = Header(section);
        var icon = new SymbolIcon(Enum.Parse<SymbolRegular>(section.Icon)) { FontSize = 24 };
        if (section.Children.Length > 0)
        {
            var children = new StackPanel();
            foreach (var child in section.Children) children.Children.Add(Build(child, true));
            return new CardExpander
            {
                Header = header, Icon = icon, Content = children, ContentPadding = new Thickness(0),
                IsExpanded = section.IsExpanded, Margin = new Thickness(0, 0, 0, nested ? 0 : 8)
            };
        }
        var editors = new Grid { HorizontalAlignment = HorizontalAlignment.Right };
        var columns = section.Columns > 0 ? section.Columns : section.Fields.Length;
        for (var i = 0; i < columns; i++) editors.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < section.Fields.Length; i++)
        {
            if (i % columns == 0) editors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var editor = Editor(section.Fields[i], section.Title);
            editor.Margin = new Thickness(i % columns == 0 ? 0 : 5, i < columns ? 0 : 5, 0, 0);
            Grid.SetColumn(editor, i % columns); Grid.SetRow(editor, i / columns);
            editors.Children.Add(editor);
        }
        return new CardControl { Header = header, Icon = icon, Content = editors, Margin = new Thickness(0, 0, 0, nested ? 0 : 8) };
    }

    private static StackPanel Header(SettingSection section)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        heading.Children.Add(new TextBlock { Text = section.Title, VerticalAlignment = VerticalAlignment.Center });
        var reset = new SettingResetButton { Properties = string.Join(',', section.Properties) };
        System.Windows.Automation.AutomationProperties.SetName(reset, $"还原{section.Title}默认设置");
        heading.Children.Add(reset);
        var panel = new StackPanel(); panel.Children.Add(heading);
        if (!string.IsNullOrEmpty(section.Description)) panel.Children.Add(Description(section.Description));
        if (section.StatusProperty is { } status)
        {
            var text = Description(""); text.Margin = new Thickness(0,4,0,0);
            text.SetBinding(TextBlock.TextProperty, new Binding(status));
            var style = new Style(typeof(TextBlock));
            var trigger = new DataTrigger { Binding = new Binding(status), Value = "" };
            trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            style.Triggers.Add(trigger); text.Style = style;
            panel.Children.Add(text);
        }
        return panel;
    }

    private static TextBlock Description(string content)
    {
        var text = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return text;
    }

    private FrameworkElement Editor(SettingField field, string title)
    {
        FrameworkElement editor;
        switch (field.Kind)
        {
            case "choice":
                var combo = new ComboBox();
                foreach (var option in field.Options) combo.Items.Add(new ComboBoxItem { Content = option });
                combo.SetBinding(Selector.SelectedIndexProperty, Value(field.Property)); editor = combo;
                break;
            case "number":
                var number = new NumberBox
                {
                    Minimum = field.Minimum, Maximum = field.Maximum, SmallChange = field.SmallChange,
                    LargeChange = field.LargeChange, ClearButtonEnabled = false
                };
                number.SetBinding(NumberBox.ValueProperty, Value(field.Property));
                number.ValueChanged += (_, _) => number.GetBindingExpression(NumberBox.ValueProperty)?.UpdateSource();
                editor = number;
                break;
            case "text":
            case "font":
            case "directory":
                var text = new Wpf.Ui.Controls.TextBox
                {
                    AcceptsReturn = false, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap,
                    ClearButtonEnabled = false, Width = field.Width
                };
                text.SetBinding(System.Windows.Controls.TextBox.TextProperty, Value(field.Property));
                editor = text;
                if (field.Kind is "font" or "directory")
                {
                    var choose = new Wpf.Ui.Controls.Button { Content = "选择", Margin = new Thickness(5,0,0,0) };
                    choose.Click += async (_, args) =>
                    {
                        args.Handled = true;
                        string selected;
                        if (field.Kind == "directory")
                        {
                            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择下载文件存储目录" };
                            if (System.IO.Directory.Exists(text.Text)) dialog.InitialDirectory = text.Text;
                            selected = dialog.ShowDialog(Window.GetWindow(text)) == true ? dialog.FolderName : "";
                        }
                        else selected = await selectFont(text.Text);
                        if (!string.IsNullOrEmpty(selected)) text.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, selected);
                    };
                    var fonts = new StackPanel { Orientation = Orientation.Horizontal };
                    fonts.Children.Add(text); fonts.Children.Add(choose); editor = fonts;
                }
                break;
            case "toggle":
                var toggle = new ToggleSwitch { VerticalAlignment = VerticalAlignment.Center };
                toggle.SetBinding(ToggleButton.IsCheckedProperty, Value(field.Property));
                var labeled = new StackPanel { Orientation = Orientation.Horizontal };
                labeled.Children.Add(new TextBlock { Text = field.Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,5,0) });
                labeled.Children.Add(toggle); editor = labeled;
                break;
            case "check":
                var check = new CheckBox { Content = field.Label, VerticalAlignment = VerticalAlignment.Center };
                check.SetBinding(ToggleButton.IsCheckedProperty, Value(field.Property)); editor = check;
                break;
            default: throw new InvalidOperationException($"未知设置控件：{field.Kind}");
        }
        if (field.Kind is not ("font" or "directory")) editor.Width = field.Width;
        if (field.VisibilityProperty is { } visibility) editor.SetBinding(UIElement.VisibilityProperty, new Binding(visibility));
        if (field.EnabledProperty is { } enabled) editor.SetBinding(UIElement.IsEnabledProperty, new Binding(enabled));
        System.Windows.Automation.AutomationProperties.SetName(editor, string.IsNullOrEmpty(field.Label) ? title : $"{title}：{field.Label}");
        return editor;
    }

    private static Binding Value(string property) => new(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
}
