using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;

namespace SekaiToolsGUI.View.Download.Components.Unit;

public partial class UnitStoryChapter : UserControl
{
    public UnitStoryChapter(UnitStorySet.Chapter chapter)
    {
        InitializeComponent();

        TitleTextBlock.Text = chapter.Name;
        foreach (var episode in chapter.Episodes)
        {
            var item = DownloadItem.GetItem(
                () => SourceList.Instance.UnitStory(episode.ScenarioId, chapter.AssetBundleName),
                episode.Title, episode.EpisodeNoLabel,
                $"UnitStory|{chapter.Name}|{episode.EpisodeNoLabel}-{episode.Title}"
            );
            item.Margin = new Thickness(0, 0, 0, 5);
            CardContents.Children.Add(item);
        }
    }
}