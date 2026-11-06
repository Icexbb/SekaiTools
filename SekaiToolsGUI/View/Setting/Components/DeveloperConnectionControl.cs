using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace SekaiToolsGUI.View.Setting.Components;

/// <summary>Full-width connection rows keep long credentials out of setting-card headers.</summary>
public sealed class DeveloperConnectionControl : CardExpander
{
    public DeveloperConnectionControl()
    {
        SetResourceReference(StyleProperty, typeof(CardExpander));
        Header = "HTTP API";
        Icon = new SymbolIcon(SymbolRegular.Beaker24);
        IsExpanded = true;
        Margin = new Thickness(0, 0, 0, 16);
        ContentPadding = new Thickness(16);
        var body = new StackPanel();
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(SettingPageModel.DeveloperApiConnectionStatus)));
        body.Children.Add(status);
        AddRow(body, "监听地址", nameof(SettingPageModel.DeveloperApiAddress));
        AddRow(body, "访问令牌", nameof(SettingPageModel.DeveloperApiToken));
        Content = body;
    }

    private static void AddRow(StackPanel body, string label, string property)
    {
        body.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) });
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var value = new TextBox
        {
            IsReadOnly = true,
            ClearButtonEnabled = false,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinWidth = 0
        };
        value.SetBinding(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.OneWay });
        AutomationProperties.SetName(value, label);
        var copy = new Button
        {
            Content = "复制",
            Icon = new SymbolIcon(SymbolRegular.Copy24),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = $"复制{label}"
        };
        copy.SetBinding(IsEnabledProperty, new Binding(nameof(SettingPageModel.IsDeveloperApiConnected)));
        AutomationProperties.SetName(copy, $"复制{label}");
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(value.Text);
                copy.ToolTip = $"已复制{label}";
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                copy.ToolTip = "剪贴板暂时不可用，请选择文本后手动复制";
            }
        };
        Grid.SetColumn(copy, 1);
        row.Children.Add(value);
        row.Children.Add(copy);
        body.Children.Add(row);
    }
}
