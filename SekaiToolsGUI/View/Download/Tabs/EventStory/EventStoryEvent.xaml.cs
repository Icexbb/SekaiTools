using System.Windows.Controls;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.View.Download.Tabs.UnitStory;

namespace SekaiToolsGUI.View.Download.Tabs.EventStory;

public partial class EventStoryEvent : UserControl
{
    private ListEventStory.EventStoryImpl _esData;

    public EventStoryEvent(ListEventStory.EventStoryImpl eventStoryImpl, SourceList.SourceType sourceType)
    {
        InitializeComponent();
        _esData = eventStoryImpl;
        DataContext = this;
        TextBlockTitle.Text = $"No.{_esData.EventStory.EventId} {eventStoryImpl.GameEvent.Name}";

        for (var i = 0; i < eventStoryImpl.EventStory.EventStoryEpisodes.Length; i++)
        {
            var episode = eventStoryImpl.EventStory.EventStoryEpisodes[i];
            var item = new DownloadItem(_esData.Url(i, sourceType), $"{episode.EpisodeNo} - {episode.Title}")
            {
                Margin = new(10, 5, 10, 5)
            };
        }
    }
}