using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiToolsGUI.View.Download.Components.Event;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadChoice : UserControl
{
    private DownloadPage? _page;
    private string? _categoryRootId;
    private (string Source, string Url)? _automationIdentity;

    public DownloadChoice()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DependencyObject? parent = this;
            while (parent != null && parent is not DownloadPage)
            {
                var automationId = AutomationProperties.GetAutomationId(parent);
                if (automationId.StartsWith("StoryDownload.", StringComparison.Ordinal)
                    && automationId.EndsWith(".Root", StringComparison.Ordinal))
                    _categoryRootId = automationId;
                parent = VisualTreeHelper.GetParent(parent);
            }
            _page = parent as DownloadPage;
            if (_page != null) _page.DownloadChoicesChanged += Page_OnChanged;
            RefreshState();
        };
        Unloaded += (_, _) =>
        {
            if (_page != null) _page.DownloadChoicesChanged -= Page_OnChanged;
            _page = null;
            _categoryRootId = null;
            _automationIdentity = null;
        };
    }

    public Func<DownloadCandidate?> CandidateFactory { get; set; } = () => null;

    private void Page_OnChanged(object? sender, EventArgs e)
    {
        RefreshState();
    }

    public void RefreshState()
    {
        var candidate = CandidateFactory();
        if (_page == null || candidate == null) return;
        var identity = (_page.GetSourceType().SourceTemplate, candidate.Url);
        if (_categoryRootId != null && _automationIdentity != identity)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{identity.SourceTemplate}\n{identity.Url}")));
            var category = _categoryRootId["StoryDownload.".Length..^".Root".Length];
            AutomationProperties.SetAutomationId(ChoiceBox, $"StoryDownload.Choice.{category}.{hash}");
            _automationIdentity = identity;
        }
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
        "Query", typeof(string), typeof(StoryBrowserState), new FrameworkPropertyMetadata("",
            FrameworkPropertyMetadataOptions.Inherits,
            (target, _) =>
            {
                if (target is DownloadItem item) item.ApplyVisibility();
                if (target is EventStoryEvent story) story.ApplySearchPresentation();
            }));

    public static string GetQuery(DependencyObject target)
    {
        return (string)target.GetValue(QueryProperty);
    }

    public static void SetQuery(DependencyObject target, string value)
    {
        target.SetValue(QueryProperty, value);
    }
}
