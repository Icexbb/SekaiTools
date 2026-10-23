using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Setting.Components;

/// <summary>Resets only the properties owned by the adjacent setting card.</summary>
public sealed class SettingResetButton : Wpf.Ui.Controls.Button
{
    public static readonly DependencyProperty PropertiesProperty = DependencyProperty.Register(
        nameof(Properties), typeof(string), typeof(SettingResetButton),
        new PropertyMetadata("", (sender, _) => ((SettingResetButton)sender).RefreshVisibility()));

    public string Properties
    {
        get => (string)GetValue(PropertiesProperty);
        set => SetValue(PropertiesProperty, value);
    }

    private SettingPageModel? _model;
    public SettingResetButton()
    {
        SetResourceReference(StyleProperty, typeof(Wpf.Ui.Controls.Button));
        Appearance = ControlAppearance.Transparent;
        Icon = new SymbolIcon(SymbolRegular.ArrowUndo24) { FontSize = 16 };
        Width = 24;
        Height = 24;
        Padding = new Thickness(4);
        Margin = new Thickness(6, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Visibility = Visibility.Collapsed;
        ToolTip = "还原此项默认设置";
        System.Windows.Automation.AutomationProperties.SetName(this, "还原此项默认设置");
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) => { if (IsLoaded) Attach(); };
        Click += (_, args) =>
        {
            args.Handled = true;
            _model?.ResetProperties(Names());
            RefreshVisibility();
        };
    }

    private string[] Names() => Properties.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private void Attach()
    {
        Detach();
        _model = DataContext as SettingPageModel;
        if (_model != null) PropertyChangedEventManager.AddHandler(_model, OnChanged, "");
        RefreshVisibility();
    }
    private void Detach()
    {
        if (_model != null) PropertyChangedEventManager.RemoveHandler(_model, OnChanged, "");
        _model = null;
    }
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => RefreshVisibility();
    private void RefreshVisibility() => Visibility = _model != null && _model.ArePropertiesModified(Names())
        ? Visibility.Visible : Visibility.Collapsed;
}
