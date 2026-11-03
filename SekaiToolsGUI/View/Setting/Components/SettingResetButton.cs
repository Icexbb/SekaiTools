using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace SekaiToolsGUI.View.Setting.Components;

/// <summary>Resets only the properties owned by the adjacent setting card.</summary>
public sealed class SettingResetButton : Button
{
    public static readonly DependencyProperty PropertiesProperty = DependencyProperty.Register(
        nameof(Properties), typeof(string), typeof(SettingResetButton),
        new PropertyMetadata("", (sender, _) => ((SettingResetButton)sender).RefreshVisibility()));

    private SettingPageModel? _model;

    public SettingResetButton()
    {
        // A content-only template keeps theme button chrome out of this inline action.
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        Template = new ControlTemplate(typeof(SettingResetButton)) { VisualTree = presenter };
        var icon = new SymbolIcon(SymbolRegular.ArrowUndo24) { FontSize = 16 };
        icon.SetResourceReference(ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        Content = icon;
        Cursor = Cursors.Hand;
        Width = 24;
        Height = 24;
        Padding = new Thickness(0);
        BorderThickness = new Thickness(0);
        Margin = new Thickness(8, 0, 0, 0);
        VerticalAlignment = VerticalAlignment.Center;
        Visibility = Visibility.Collapsed;
        ToolTip = "还原此项默认设置";
        AutomationProperties.SetName(this, "还原此项默认设置");
        Loaded += (_, _) => Attach();
        Unloaded += (_, _) => Detach();
        DataContextChanged += (_, _) =>
        {
            if (IsLoaded) Attach();
        };
        Click += (_, args) =>
        {
            args.Handled = true;
            _model?.ResetProperties(Names());
            RefreshVisibility();
        };
    }

    public string Properties
    {
        get => (string)GetValue(PropertiesProperty);
        set => SetValue(PropertiesProperty, value);
    }

    private string[] Names()
    {
        return Properties.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

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

    private void OnChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        Visibility = _model != null && _model.ArePropertiesModified(Names())
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}