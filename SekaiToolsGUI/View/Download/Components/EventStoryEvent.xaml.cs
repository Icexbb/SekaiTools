using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SekaiDataFetch;
using SekaiDataFetch.List;

namespace SekaiToolsGUI.View.Download.Components;

public partial class EventStoryEvent : UserControl
{
    public EventStoryEvent(ListEventStory.EventStoryImpl eventStoryImpl, SourceList.SourceType sourceType)
    {
        EventStoryImpl = eventStoryImpl;
        InitializeComponent();
        TextBlockTitle.Text = $"No.{EventStoryImpl.EventStory.EventId} {EventStoryImpl.GameEvent.Name}";
        ImageBannerIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resource/Characters/" +
                    $"chr_{EventStoryImpl.EventStory.BannerGameCharacterUnitId}.png"));
        for (var i = 0; i < EventStoryImpl.EventStory.EventStoryEpisodes.Length; i++)
        {
            var episode = EventStoryImpl.EventStory.EventStoryEpisodes[i];
            var item = new DownloadItem(EventStoryImpl.Url(i, sourceType),
                $"{EventStoryImpl.EventStory.EventId} - {episode.EpisodeNo} : {episode.Title}")
            {
                Margin = new Thickness(10, 5, 10, 5)
            };
            PanelItems.Children.Add(item);
        }
    }

    public ListEventStory.EventStoryImpl EventStoryImpl { get; }
}