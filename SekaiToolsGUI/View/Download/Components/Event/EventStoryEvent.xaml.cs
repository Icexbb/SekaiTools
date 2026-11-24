using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryEvent : UserControl
{
    public static readonly DependencyProperty EventStoryImplProperty =
        DependencyProperty.Register(
            nameof(EventStorySet),
            typeof(EventStorySet),
            typeof(EventStoryEvent),
            new PropertyMetadata(null, OnEventStoryImplChanged));

    public EventStorySet? EventStorySet
    {
        get => (EventStorySet?)GetValue(EventStoryImplProperty);
        set => SetValue(EventStoryImplProperty, value);
    }

    private static void OnEventStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventStoryEvent)d;
        control.ResetBanner();
        if (control.EventStorySet == null) return;
        control.RefreshControl();
        _ = control.LoadBannerAsync();
    }
}

public partial class EventStoryEvent
{
    private CancellationTokenSource? _bannerRequest;

    public EventStoryEvent()
    {
        InitializeComponent();
        Margin = new Thickness(0, 0, 0, 5);
        Loaded += (_, _) =>
        {
            ApplySearchPresentation();
            _ = LoadBannerAsync();
        };
        Unloaded += (_, _) => ResetBanner();
    }

    private void ResetBanner()
    {
        _bannerRequest?.Cancel();
        _bannerRequest = null;
        EventBannerImage.Source = null;
        EventBannerImage.Visibility = Visibility.Collapsed;
    }

    private async Task LoadBannerAsync()
    {
        if (!IsLoaded || EventStorySet is not { } story || _bannerRequest != null || EventBannerImage.Source != null)
            return;

        var urls = DownloadPageModel.Instance.OrderedSources
            .Select(source => new SourceList(source).EventBanner(story.GameEvent.AssetBundleName, story.EventStory.AssetBundleName))
            .OfType<Uri>().Distinct().ToArray();
        if (urls.Length == 0) return;

        using var request = new CancellationTokenSource();
        _bannerRequest = request;
        try
        {
            var banner = await StoryArtworkCache.LoadThumbnailAsync(urls, request.Token, "EventBannerCache");
            if (_bannerRequest != request || banner == null) return;
            EventBannerImage.Source = banner;
            AutomationProperties.SetName(EventBannerImage, story.GameEvent.Name);
            EventBannerImage.Visibility = Visibility.Visible;
        }
        finally
        {
            if (_bannerRequest == request) _bannerRequest = null;
        }
    }

    private void RefreshControl()
    {
        if (EventStorySet == null) return;
        IndexInfoBadge.Value = EventStorySet.Index.ToString();
        IndexInfoBadgeEx.Value = EventStorySet.EventStory.EventId.ToString();
        IndexInfoBadgeEx.Visibility = EventStorySet.Index != EventStorySet.EventStory.EventId
            ? Visibility.Visible
            : Visibility.Collapsed;
        TextBlockTitle.Text = $"{EventStorySet.GameEvent.Name}";
        AddAllButton.IsEnabled = EventStorySet.EventStory.EventStoryEpisodes.Length > 0;

        RefreshBannerIcons();

        InitDownloadItems();
    }

    internal void ApplySearchPresentation()
    {
        var searching = !string.IsNullOrWhiteSpace(StoryBrowserState.GetQuery(this));
        if (searching) ChaptersExpander.IsExpanded = true;
        AddAllText.Text = searching ? "添加结果" : "添加全部";
        AddAllButton.ToolTip = searching ? "将本活动匹配的章节加入下载列表" : "将本活动全部章节加入下载列表";
    }

    private void AddAll_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (EventStorySet is not { } story) return;
        DependencyObject? parent = this;
        while (parent != null && parent is not DownloadPage)
            parent = VisualTreeHelper.GetParent(parent);
        if (parent is not DownloadPage page) return;
        var source = page.GetSourceType();
        var query = StoryBrowserState.GetQuery(this);
        page.AddTasks(StoryDownloadCandidates.Event(story, source).Where(candidate => candidate.Matches(query)));
    }

    private void RefreshBannerIcons()
    {
        IconContainer.Children.Clear();

        var bannerCharacterIds = EventStorySet!.BannerGameCharacterIds.Length > 0
            ? EventStorySet.BannerGameCharacterIds
            : EventStorySet.EventStory.BannerGameCharacterUnitId is { } bannerCharacterId
                ? [bannerCharacterId]
                : [];

        foreach (var characterId in bannerCharacterIds.Distinct())
            IconContainer.Children.Add(new Image
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 5, 0),
                Source = new BitmapImage(
                    new Uri(
                        $"pack://application:,,,/SekaiToolsGUI;component/Resource/Characters/chr_{characterId}.png"))
            });
    }

    private void InitDownloadItems()
    {
        var children = PanelItems.Children.OfType<UIElement>().ToList();
        foreach (var panelItemsChild in children)
            if (panelItemsChild is DownloadItem downloadItem)
                downloadItem.Recycle();

        const int itemPreRow = 4;
        PanelItems.RowDefinitions.Clear();
        PanelItems.ColumnDefinitions.Clear();
        for (var column = 0; column < itemPreRow; column++)
            PanelItems.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < EventStorySet!.EventStory.EventStoryEpisodes.Length; i++)
        {
            var storyIndex = EventStorySet.Index;
            var episode = EventStorySet.EventStory.EventStoryEpisodes[i];
            var key = $"{storyIndex}-{episode.EpisodeNo}";

            var downloadItem = DownloadItem.GetItem(source =>
                    new SourceList(source).EventStory(episode.ScenarioId,
                        EventStorySet.EventStory.AssetBundleName), episode.Title, key,
                $"EventStory|{storyIndex}-{EventStorySet.GameEvent.Name}|{episode.EpisodeNo}-{episode.Title}",
                EventStorySet.EventStory.EventId.ToString());

            if (i % itemPreRow == 0)
                PanelItems.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(downloadItem, i / itemPreRow);
            Grid.SetColumn(downloadItem, i % itemPreRow);
            downloadItem.Margin = new Thickness(0, 5, 5, 0);
            PanelItems.Children.Add(downloadItem);
        }
    }
}
