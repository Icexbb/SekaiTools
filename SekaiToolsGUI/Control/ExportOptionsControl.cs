using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.View.Setting;

namespace SekaiToolsGUI.Control;

/// <summary>Shared export editor; the inherited DataContext owns persistence and option values.</summary>
public sealed class ExportOptionsControl : UserControl
{
    private readonly SettingSection _section;

    public static readonly DependencyProperty ShowResetButtonsProperty = DependencyProperty.Register(
        nameof(ShowResetButtons), typeof(bool), typeof(ExportOptionsControl),
        new PropertyMetadata(false, OnPresentationChanged));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ExportOptionsControl),
        new PropertyMetadata("选择本次输出的类别，仅对本次输出生效", OnPresentationChanged));
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(ExportOptionsControl),
        new PropertyMetadata(true, OnPresentationChanged));

    public bool ShowResetButtons
    {
        get => (bool)GetValue(ShowResetButtonsProperty);
        set => SetValue(ShowResetButtonsProperty, value);
    }
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ExportOptionsControl() : this(SettingCatalog.Load().First(IsExportSection)) { }

    internal ExportOptionsControl(SettingSection section)
    {
        _section = section;
        Rebuild();
    }

    internal static bool IsExportSection(SettingSection section) => section.Id == "subtitle-export" ||
        section.Properties.Contains("ExportLine1"); // Compatibility with older external catalogs.

    private static void OnPresentationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ExportOptionsControl)sender).Rebuild();

    private void Rebuild()
    {
        if (_section == null) return;
        var section = new SettingSection
        {
            Title = _section.Title,
            Icon = _section.Icon,
            Description = Description,
            IsExpanded = IsExpanded,
            Children = _section.Children
        };
        var builder = new SettingViewBuilder(_ => throw new InvalidOperationException("输出选项不包含字体选择"),
            _ => throw new InvalidOperationException("输出选项不包含自定义区域"))
        {
            ShowResetButtons = ShowResetButtons
        };
        Content = builder.Build(section);
    }
}
