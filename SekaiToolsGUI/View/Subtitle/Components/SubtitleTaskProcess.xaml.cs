using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsGUI.ViewModel.Subtitle;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTaskProcess : UserControl
{
    private bool _shortLayout;
    private double _requestedPreviewHeight = 200;

    private double MaximumPreviewHeight => Math.Max(200, Math.Min(ActualHeight,
        (ActualWidth - 350 - 5 + 12) / 2));

    private void UpdatePreviewSize()
    {
        if (FramePreviewCard == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var height = Math.Clamp(_requestedPreviewHeight, 200, MaximumPreviewHeight);
        FramePreviewCard.Height = height;
        // The card's padding and border occupy 12px; its image viewport stays at 2:1.
        FramePreviewCard.Width = 2 * (height - 12) + 12;
        PreviewZoomOutButton.IsEnabled = height > 200;
        PreviewZoomInButton.IsEnabled = height < MaximumPreviewHeight;
    }

    private void ChangePreviewHeight(double delta)
    {
        _requestedPreviewHeight = Math.Clamp(FramePreviewCard.Height + delta, 200, MaximumPreviewHeight);
        UpdatePreviewSize();
    }

    private void SubtitleTaskProcess_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreviewSize();
    private void PreviewZoomOutButton_OnClick(object sender, RoutedEventArgs e) => ChangePreviewHeight(-50);
    private void PreviewZoomInButton_OnClick(object sender, RoutedEventArgs e) => ChangePreviewHeight(50);

    private void FramePreviewCard_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ChangePreviewHeight(Math.Sign(e.Delta) * 50);
        e.Handled = true;
    }

    public SubtitleTaskProcess()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler? StopRequested;
    public event RoutedEventHandler? OutputRequested;
    public event RoutedEventHandler? ResetRequested;

    private SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;

    private void StopButton_OnClick(object sender, RoutedEventArgs e) => StopRequested?.Invoke(sender, e);

    private void OutputButton_OnClick(object sender, RoutedEventArgs e) => OutputRequested?.Invoke(sender, e);

    private void ResetButton_OnClick(object sender, RoutedEventArgs e) => ResetRequested?.Invoke(sender, e);

    internal void UpdateViewport(double height)
    {
        var shortLayout = height < 700;
        if (shortLayout && !_shortLayout && EventTimelineEditor != null)
            EventTimelineEditor.ViewModel.ShowTimeLine = false;
        if (TimelineViewport != null)
            TimelineViewport.MaxHeight = shortLayout ? Math.Max(60, height - 330) : double.PositiveInfinity;
        _shortLayout = shortLayout;
    }

    internal void RefreshContentVisibility()
    {
        foreach (var child in LinePanel.Children)
            switch (child)
            {
                case DialogLine dialogLine:
                    var lineCount = dialogLine.ViewModel.RawContent.Split("\n").Length;
                    dialogLine.Visibility = lineCount switch
                    {
                        1 => ViewModel is { ShowDialog: true, ShowDialogLine1: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        2 => ViewModel is { ShowDialog: true, ShowDialogLine2: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        3 => ViewModel is { ShowDialog: true, ShowDialogLine3: true }
                            ? Visibility.Visible
                            : Visibility.Collapsed,
                        _ => dialogLine.Visibility
                    };
                    break;
                case BannerLine bannerLine:
                    bannerLine.Visibility = ViewModel.ShowBanner ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case MarkerLine markerLine:
                    markerLine.Visibility = ViewModel.ShowMarker ? Visibility.Visible : Visibility.Collapsed;
                    break;
            }
    }

    private void DialogFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialog = !ViewModel.ShowDialog;
        RefreshContentVisibility();
    }

    private void DialogFilterLine1Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine1 = !ViewModel.ShowDialogLine1;
        RefreshContentVisibility();
        e.Handled = true;
    }

    private void DialogFilterLine2Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine2 = !ViewModel.ShowDialogLine2;
        RefreshContentVisibility();
        e.Handled = true;
    }

    private void DialogFilterLine3Btn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowDialogLine3 = !ViewModel.ShowDialogLine3;
        RefreshContentVisibility();
        e.Handled = true;
    }

    private void BannerFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowBanner = !ViewModel.ShowBanner;
        RefreshContentVisibility();
    }

    private void MarkerFilterBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowMarker = !ViewModel.ShowMarker;
        RefreshContentVisibility();
    }

    private void VideoFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.VideoFilePath);
    }

    private void ScriptFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.ScriptFilePath);
    }

    private void TranslateFileBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ExplorerHelper.OpenFolderAndFocus(ViewModel.TranslateFilePath);
    }

    private void BackToTopBtn_OnClick(object sender, RoutedEventArgs e)
    {
        LineViewer.ScrollToTop();
    }

    private void PreviewToggleBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowPreview = !ViewModel.ShowPreview;
    }

    private void BackToBottomBtn_OnClick(object sender, RoutedEventArgs e)
    {
        LineViewer.ScrollToBottom();
    }

    private void SubtitleTaskProcess_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Z ||
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            Keyboard.FocusedElement is TextBox)
            return;

        if (GeneralFunctionSwitch.EventTimeline && EventTimelineEditor.Undo())
            e.Handled = true;
    }
    private void Control_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.ShowPreview = false;
    }
    private void ShowPreviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ShowPreview = true;
    }
}
