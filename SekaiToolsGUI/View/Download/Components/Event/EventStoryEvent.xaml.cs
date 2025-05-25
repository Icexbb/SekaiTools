using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryEvent : UserControl
{
    public static readonly DependencyProperty EventStoryImplProperty =
        DependencyProperty.Register(
            nameof(EventStorySet),
            typeof(EventStorySet),
            typeof(EventStoryEvent),
            new PropertyMetadata(null, OnEventStoryImplChanged));

    private static void OnEventStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EventStoryEvent control && e.NewValue is EventStorySet eventStoryImpl)
            control.Initialize(eventStoryImpl);
    }

    public EventStoryEvent()
    {
        InitializeComponent();
        Margin = new Thickness(5);
    }

    private EventStoryEvent(EventStorySet eventStorySet)
    {
        InitializeComponent();
        Margin = new Thickness(5);

        EventStorySet = eventStorySet;
        Initialize(eventStorySet);
    }

    public EventStorySet? EventStorySet
    {
        get => (EventStorySet?)GetValue(EventStoryImplProperty);
        set => SetValue(EventStoryImplProperty, value);
    }
}

public partial class EventStoryEvent
{
    private static List<EventStoryEvent> RecycleContainer { get; } = [];

    private void Initialize(EventStorySet eventStorySet)
    {
        EventStorySet = eventStorySet;
        TextBlockTitle.Text = $"No.{EventStorySet.EventStory.EventId} {EventStorySet.GameEvent.Name}";
        ImageBannerIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resource/Characters/" +
                    $"chr_{EventStorySet.EventStory.BannerGameCharacterUnitId}.png"));
        InitDownloadItems();
    }

    public static void RecycleItem(EventStoryEvent item)
    {
        item.Visibility = Visibility.Collapsed;
        RecycleContainer.Add(item);
        (item.Parent as Panel)?.Children.Remove(item);
    }

    public static EventStoryEvent GetItem(EventStorySet eventStorySet)
    {
        if (RecycleContainer.Count <= 0) return new EventStoryEvent(eventStorySet);
        var item = RecycleContainer[0];
        RecycleContainer.RemoveAt(0);
        item.Visibility = Visibility.Visible;
        item.Initialize(eventStorySet);
        return item;
    }
}

public partial class EventStoryEvent
{
    private void InitDownloadItems()
    {
        foreach (UIElement panelItemsChild in PanelItems.Children)
        {
            if (panelItemsChild is DownloadItem downloadItem) downloadItem.Recycle();
        }

        for (var i = 0; i < EventStorySet!.EventStory.EventStoryEpisodes.Length; i++)
        {
            var episode = EventStorySet.EventStory.EventStoryEpisodes[i];
            var key = $"{EventStorySet.EventStory.EventId} - {episode.EpisodeNo} : {episode.Title}";

            PanelItems.Children.Add(DownloadItem.GetItem(() => SourceList.Instance.EventStory(episode.ScenarioId,
                EventStorySet.EventStory.AssetBundleName), key));
        }
    }
}