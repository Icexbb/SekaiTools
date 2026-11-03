using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace SekaiToolsGUI.Control;

/// <summary>Dialog size categories shared by XAML and dynamically created dialogs.</summary>
public enum DialogPurpose
{
    Confirmation,
    CompactEditor,
    TranslationExport,
    Workspace,
    Progress
}

/// <summary>Shared size breakpoints and bounded dialog content without scaling text.</summary>
public static class AdaptiveLayout
{
    static AdaptiveLayout()
    {
        EventManager.RegisterClassHandler(typeof(ContentDialog), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnDialogLoaded));
    }

    // Called before the main window opens, so dynamically created confirmations use the same rules.
    public static void InitializeDialogSizing() { }

    public static readonly DependencyProperty DialogPurposeProperty = DependencyProperty.RegisterAttached(
        "DialogPurpose", typeof(DialogPurpose), typeof(AdaptiveLayout), new PropertyMetadata(DialogPurpose.Confirmation));

    public static void SetDialogPurpose(DependencyObject element, DialogPurpose value) => element.SetValue(DialogPurposeProperty, value);
    public static DialogPurpose GetDialogPurpose(DependencyObject element) => (DialogPurpose)element.GetValue(DialogPurposeProperty);

    public static readonly DependencyProperty MonitorSizeProperty = DependencyProperty.RegisterAttached(
        "MonitorSize", typeof(bool), typeof(AdaptiveLayout), new PropertyMetadata(false, OnMonitorSizeChanged));

    private static readonly DependencyPropertyKey IsNarrowPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsNarrow", typeof(bool), typeof(AdaptiveLayout),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsNarrowProperty = IsNarrowPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey IsShortPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsShort", typeof(bool), typeof(AdaptiveLayout),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static readonly DependencyProperty IsShortProperty = IsShortPropertyKey.DependencyProperty;

    public static readonly DependencyProperty FitDialogProperty = DependencyProperty.RegisterAttached(
        "FitDialog", typeof(bool), typeof(AdaptiveLayout), new PropertyMetadata(false));

    public static void SetMonitorSize(DependencyObject element, bool value)
    {
        element.SetValue(MonitorSizeProperty, value);
    }

    public static bool GetMonitorSize(DependencyObject element)
    {
        return (bool)element.GetValue(MonitorSizeProperty);
    }

    public static bool GetIsNarrow(DependencyObject element)
    {
        return (bool)element.GetValue(IsNarrowProperty);
    }

    public static bool GetIsShort(DependencyObject element)
    {
        return (bool)element.GetValue(IsShortProperty);
    }

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

    public static void SetFitDialog(DependencyObject element, bool value)
    {
        element.SetValue(FitDialogProperty, value);
    }

    public static bool GetFitDialog(DependencyObject element)
    {
        return (bool)element.GetValue(FitDialogProperty);
    }

    private static void OnDialogLoaded(object sender, RoutedEventArgs args)
    {
        var dialog = (ContentDialog)sender;
        var window = Window.GetWindow(dialog);
        if (window == null) return;
        FrameworkElement viewport = FindHost(dialog) is { } host ? host : window;
        // WPF-UI 4.x binds DialogWidth/Height to the surface's maximum size, not its size.
        // Keep the overlay full-size and size the inner surface explicitly.
        dialog.ApplyTemplate();
        var root = dialog.Template.FindName("DialogRootGrid", dialog) as Grid;
        var surface = root?.Children.OfType<Border>().FirstOrDefault();
        var purpose = GetDialogPurpose(dialog);
        var contentMaximumHeight = (dialog.Content as FrameworkElement)?.MaxHeight ?? double.PositiveInfinity;

        void Fit()
        {
            var availableWidth = Math.Max(1, viewport.ActualWidth - 80);
            var availableHeight = Math.Max(1, viewport.ActualHeight - 80);
            var requested = purpose switch
            {
                DialogPurpose.Workspace => new Size(viewport.ActualWidth * .7, viewport.ActualHeight * .7),
                DialogPurpose.CompactEditor => new Size(560, 360),
                DialogPurpose.TranslationExport => new Size(600, 480),
                _ => new Size(480, 280)
            };
            var width = Math.Min(requested.Width, availableWidth);
            var height = Math.Min(requested.Height, availableHeight);
            dialog.MinWidth = 0;
            dialog.MinHeight = 0;
            dialog.DialogMaxWidth = availableWidth;
            dialog.DialogMaxHeight = availableHeight;
            dialog.DialogWidth = width;
            dialog.DialogHeight = height;
            if (surface != null)
            {
                surface.Width = width;
                surface.Height = height;
            }
            if (dialog.Content is FrameworkElement content && (content is ScrollViewer || content is Grid))
                content.MaxHeight = Math.Min(contentMaximumHeight, Math.Max(1, height - (dialog.IsFooterVisible ? 160 : 64)));
        }

        Fit();
        SizeChangedEventHandler resized = (_, _) => Fit();
        viewport.SizeChanged += resized;
        RoutedEventHandler? unloaded = null;
        unloaded = (_, _) =>
        {
            viewport.SizeChanged -= resized;
            if (dialog.Content is FrameworkElement content && (content is ScrollViewer || content is Grid))
                content.MaxHeight = contentMaximumHeight;
            dialog.Unloaded -= unloaded;
        };
        dialog.Unloaded += unloaded;
    }

    private static ContentDialogHost? FindHost(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element);
             parent != null;
             parent = VisualTreeHelper.GetParent(parent))
            if (parent is ContentDialogHost host)
                return host;
        return null;
    }
}