using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using SekaiDataFetch;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadChoice : UserControl
{
    public Func<DownloadCandidate?> CandidateFactory { get; set; } = () => null;
    private DownloadPage? _page;
    public DownloadChoice()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DependencyObject? parent = this;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);
            _page = parent as DownloadPage;
            if (_page != null) _page.DownloadChoicesChanged += Page_OnChanged;
            RefreshState();
        };
        Unloaded += (_, _) =>
        {
            if (_page != null) _page.DownloadChoicesChanged -= Page_OnChanged;
            _page = null;
        };
    }
    private void Page_OnChanged(object? sender, EventArgs e) => RefreshState();
    public void RefreshState()
    {
        var candidate = CandidateFactory();
        if (_page == null || candidate == null) return;
        var queued = _page.IsDownloadQueued(candidate.Url);
        ChoiceBox.IsChecked = queued || _page.IsDownloadSelected(candidate.Url);
        ChoiceBox.IsEnabled = !queued;
        ChoiceBox.Content = _page.GetDownloadState(candidate.Url);
        AutomationProperties.SetName(ChoiceBox, "选择下载项 " + candidate.Tag);
    }
    private void Choice_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_page != null && CandidateFactory() is { } candidate)
            _page.SelectDownload(candidate, ChoiceBox.IsChecked == true);
    }
}

public static class StoryBrowserState
{
    public static readonly DependencyProperty QueryProperty = DependencyProperty.RegisterAttached(
        "Query", typeof(string), typeof(StoryBrowserState), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.Inherits,
            (target, _) =>
            {
                if (target is DownloadItem item) item.ApplyVisibility();
                if (target is Event.EventStoryEvent story) story.ApplySearchPresentation();
            }));
    public static string GetQuery(DependencyObject target) => (string)target.GetValue(QueryProperty);
    public static void SetQuery(DependencyObject target, string value) => target.SetValue(QueryProperty, value);
}
