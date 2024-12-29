using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SekaiDataFetch;
using SekaiDataFetch.List;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryEvent : UserControl
{
    public static readonly DependencyProperty EventStoryImplProperty =
        DependencyProperty.Register(
            nameof(EventStoryImpl),
            typeof(EventStoryImpl),
            typeof(EventStoryEvent),
            new PropertyMetadata(null, OnEventStoryImplChanged));

    public EventStoryEvent()
    {
        InitializeComponent();
        Margin = new Thickness(5);
    }

    private EventStoryEvent(EventStoryImpl eventStoryImpl, SourceType sourceType)
    {
        InitializeComponent();
        Margin = new Thickness(5);

        EventStoryImpl = eventStoryImpl;
        Initialize(eventStoryImpl, sourceType);
    }

    public EventStoryImpl? EventStoryImpl
    {
        get => (EventStoryImpl?)GetValue(EventStoryImplProperty);
        set => SetValue(EventStoryImplProperty, value);
    }

    private static void OnEventStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EventStoryEvent control && e.NewValue is EventStoryImpl eventStoryImpl)
            control.Initialize(eventStoryImpl);
    }
}

public partial class EventStoryEvent
{
    private static List<EventStoryEvent> RecycleContainer { get; } = [];

    private void Initialize(EventStoryImpl eventStoryImpl,
        SourceType sourceType = SourceType.SiteBest)
    {
        EventStoryImpl = eventStoryImpl;
        TextBlockTitle.Text = $"No.{EventStoryImpl.EventStory.EventId} {EventStoryImpl.GameEvent.Name}";
        ImageBannerIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resource/Characters/" +
                    $"chr_{EventStoryImpl.EventStory.BannerGameCharacterUnitId}.png"));
        InitDownloadItems(sourceType);
    }

    public static void RecycleItem(EventStoryEvent item)
    {
        item.Visibility = Visibility.Collapsed;
        RecycleContainer.Add(item);
        (item.Parent as Panel)?.Children.Remove(item);
    }

    public static EventStoryEvent GetItem(EventStoryImpl eventStoryImpl,
        SourceType sourceType)
    {
        if (RecycleContainer.Count <= 0) return new EventStoryEvent(eventStoryImpl, sourceType);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.Visibility = Visibility.Visible;
        item.Initialize(eventStoryImpl, sourceType);
        return item;
    }
}

public partial class EventStoryEvent
{
    private void InitDownloadItems(SourceType sourceType)
    {
        if (PanelItems.Children.Count > EventStoryImpl!.EventStory.EventStoryEpisodes.Length)
            for (var i = EventStoryImpl.EventStory.EventStoryEpisodes.Length; i < PanelItems.Children.Count; i++)
                DownloadItem.RecycleItem((DownloadItem)PanelItems.Children[i]);


        for (var i = 0; i < EventStoryImpl!.EventStory.EventStoryEpisodes.Length; i++)
        {
            var episode = EventStoryImpl.EventStory.EventStoryEpisodes[i];
            var url = EventStoryImpl.Url(i, sourceType);
            var key = $"{EventStoryImpl.EventStory.EventId} - {episode.EpisodeNo} : {episode.Title}";

            if (PanelItems.Children.Count <= i)
            {
                PanelItems.Children.Add(DownloadItem.GetItem(url, key));
            }
            else
            {
                var item = (DownloadItem)PanelItems.Children[i];
                item.Initialize(url, key);
            }
        }
    }
}