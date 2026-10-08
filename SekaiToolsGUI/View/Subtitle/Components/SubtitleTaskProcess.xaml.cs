using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsGUI.ViewModel.Subtitle;
using TextBox = System.Windows.Controls.TextBox;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTaskProcess : UserControl
{
    private bool _shortLayout;

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
