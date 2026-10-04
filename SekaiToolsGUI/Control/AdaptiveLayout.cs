using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace SekaiToolsGUI.Control;

/// <summary>Shared size breakpoints and bounded dialog content without scaling text.</summary>
public static class AdaptiveLayout
{
    public static readonly DependencyProperty MonitorSizeProperty = DependencyProperty.RegisterAttached(
        "MonitorSize", typeof(bool), typeof(AdaptiveLayout), new PropertyMetadata(false, OnMonitorSizeChanged));
    public static void SetMonitorSize(DependencyObject element, bool value) => element.SetValue(MonitorSizeProperty, value);
    public static bool GetMonitorSize(DependencyObject element) => (bool)element.GetValue(MonitorSizeProperty);

    private static readonly DependencyPropertyKey IsNarrowPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsNarrow", typeof(bool), typeof(AdaptiveLayout), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static readonly DependencyProperty IsNarrowProperty = IsNarrowPropertyKey.DependencyProperty;
    public static bool GetIsNarrow(DependencyObject element) => (bool)element.GetValue(IsNarrowProperty);

    private static readonly DependencyPropertyKey IsShortPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsShort", typeof(bool), typeof(AdaptiveLayout), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static readonly DependencyProperty IsShortProperty = IsShortPropertyKey.DependencyProperty;
    public static bool GetIsShort(DependencyObject element) => (bool)element.GetValue(IsShortProperty);

    private static void OnMonitorSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element) return;
        if ((bool)args.NewValue) element.SizeChanged += OnSizeChanged;
        else element.SizeChanged -= OnSizeChanged;
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var element = (FrameworkElement)sender;
        element.SetValue(IsNarrowPropertyKey, element.ActualWidth < 1100);
        element.SetValue(IsShortPropertyKey, element.ActualHeight < 700);
    }

    public static readonly DependencyProperty FitDialogProperty = DependencyProperty.RegisterAttached(
        "FitDialog", typeof(bool), typeof(AdaptiveLayout), new PropertyMetadata(false, OnFitDialogChanged));
    public static void SetFitDialog(DependencyObject element, bool value) => element.SetValue(FitDialogProperty, value);
    public static bool GetFitDialog(DependencyObject element) => (bool)element.GetValue(FitDialogProperty);

    private static void OnFitDialogChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ContentDialog dialog || !(bool)args.NewValue) return;
        dialog.Loaded += OnDialogLoaded;
    }

    private static void OnDialogLoaded(object sender, RoutedEventArgs args)
    {
        var dialog = (ContentDialog)sender;
        var window = Window.GetWindow(dialog);
        if (window == null) return;
        FrameworkElement viewport = FindHost(dialog) is { } host ? host : window;
        var minimumWidth = dialog.MinWidth;
        var minimumHeight = dialog.MinHeight;
        void Fit()
        {
            var width = Math.Max(1, viewport.ActualWidth - 32);
            var height = Math.Max(1, viewport.ActualHeight - 32);
            dialog.MinWidth = Math.Min(minimumWidth, width);
            dialog.MinHeight = Math.Min(minimumHeight, height);
            dialog.DialogMaxWidth = width;
            dialog.DialogMaxHeight = height;
            if (dialog.Content is FrameworkElement content && (content is ScrollViewer || content is Grid))
                content.MaxHeight = Math.Max(1, height - 240); // Reserve title, template padding and action buttons.
        }
        // WPF-UI already scrolls plain content; bound existing viewers and grids only.
        Fit();
        SizeChangedEventHandler resized = (_, _) => Fit();
        viewport.SizeChanged += resized;
        RoutedEventHandler? unloaded = null;
        unloaded = (_, _) =>
        {
            viewport.SizeChanged -= resized;
            dialog.Unloaded -= unloaded;
        };
        dialog.Unloaded += unloaded;
    }

    private static ContentDialogHost? FindHost(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ContentDialogHost host) return host;
        return null;
    }
}
