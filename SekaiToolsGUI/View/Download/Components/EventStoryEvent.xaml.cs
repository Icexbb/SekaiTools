using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SekaiDataFetch;
using SekaiDataFetch.List;

namespace SekaiToolsGUI.View.Download.Components;

public partial class EventStoryEvent : UserControl
{
    public EventStoryImpl? EventStoryImpl { get; private set; }

    public EventStoryEvent(EventStoryImpl eventStoryImpl, SourceList.SourceType sourceType)
    {
        InitializeComponent();
        Margin = new Thickness(5);

        Initialize(eventStoryImpl, sourceType);
    }
}

public partial class EventStoryEvent
{
    public void Initialize(EventStoryImpl eventStoryImpl, SourceList.SourceType sourceType)
    {
        EventStoryImpl = eventStoryImpl;
        TextBlockTitle.Text = $"No.{EventStoryImpl.EventStory.EventId} {EventStoryImpl.GameEvent.Name}";
        ImageBannerIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resource/Characters/" +
                    $"chr_{EventStoryImpl.EventStory.BannerGameCharacterUnitId}.png"));
        InitDownloadItems(sourceType);
    }

    private static List<EventStoryEvent> RecycleContainer { get; } = [];

    public static void RecycleItem(EventStoryEvent item)
    {
        item.Visibility = Visibility.Collapsed;
        RecycleContainer.Add(item);
        (item.Parent as Panel)?.Children.Remove(item);
    }

    public static EventStoryEvent GetItem(EventStoryImpl eventStoryImpl,
        SourceList.SourceType sourceType)
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
    private void InitDownloadItems(SourceList.SourceType sourceType)
    {
        if (PanelItems.Children.Count > EventStoryImpl!.EventStory.EventStoryEpisodes.Length)
        {
            for (var i = EventStoryImpl.EventStory.EventStoryEpisodes.Length; i < PanelItems.Children.Count; i++)
            {
                DownloadItem.RecycleItem((DownloadItem)PanelItems.Children[i]);
            }
        }


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