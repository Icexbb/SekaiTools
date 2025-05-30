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
        if (d is not EventStoryEvent control || e.NewValue is not EventStorySet eventStorySet) return;
        control.EventStorySet = eventStorySet;
        control.RefreshControl();
    }

    public EventStorySet? EventStorySet
    {
        get => (EventStorySet?)GetValue(EventStoryImplProperty);
        set => SetValue(EventStoryImplProperty, value);
    }
}

public partial class EventStoryEvent
{
    public static readonly DependencyProperty UseStoryIndexProperty =
        DependencyProperty.Register(
            nameof(UseStoryIndex),
            typeof(bool),
            typeof(EventStoryEvent),
            new PropertyMetadata(false, OnUseStoryIndexChanged));

    private static void OnUseStoryIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not EventStoryEvent control || e.NewValue is not bool useStoryIndex) return;
        control.UseStoryIndex = useStoryIndex;
        control.RefreshControl();
    }

    public bool UseStoryIndex
    {
        get => (bool)GetValue(UseStoryIndexProperty);
        set => SetValue(UseStoryIndexProperty, value);
    }
}

public partial class EventStoryEvent
{
    public EventStoryEvent()
    {
        InitializeComponent();
        Margin = new Thickness(5);
    }


    private void RefreshControl()
    {
        if (EventStorySet == null) return;
        var storyIndex = UseStoryIndex ? EventStorySet.EventStory.EventId : EventStorySet.Index;
        TextBlockTitle.Text = $"No.{storyIndex} {EventStorySet.GameEvent.Name}";

        ImageBannerIcon.Source = new BitmapImage(
            new Uri($"pack://application:,,,/Resource/Characters/" +
                    $"chr_{EventStorySet.EventStory.BannerGameCharacterUnitId}.png"));

        InitDownloadItems();
    }

    private void InitDownloadItems()
    {
        var children = PanelItems.Children.OfType<UIElement>().ToList();
        foreach (var panelItemsChild in children)
        {
            if (panelItemsChild is DownloadItem downloadItem) downloadItem.Recycle();
        }

        for (var i = 0; i < EventStorySet!.EventStory.EventStoryEpisodes.Length; i++)
        {
            var storyIndex = UseStoryIndex ? EventStorySet.EventStory.EventId : EventStorySet.Index;
            var episode = EventStorySet.EventStory.EventStoryEpisodes[i];
            var key = $"{storyIndex} - {episode.EpisodeNo} : {episode.Title}";

            PanelItems.Children.Add(DownloadItem.GetItem(() => SourceList.Instance.EventStory(episode.ScenarioId,
                EventStorySet.EventStory.AssetBundleName), key));
        }
    }
}