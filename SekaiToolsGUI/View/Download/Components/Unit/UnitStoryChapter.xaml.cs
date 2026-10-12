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
        const int itemPreRow = 4;
        for (var column = 0; column < itemPreRow; column++)
            CardContents.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var index = 0;
        foreach (var episode in chapter.Episodes)
        {
            var item = DownloadItem.GetItem(
                () => SourceList.Instance.UnitStory(episode.ScenarioId, chapter.AssetBundleName),
                episode.Title, episode.EpisodeNoLabel,
                $"UnitStory|{chapter.Name}|{episode.EpisodeNoLabel}-{episode.Title}"
            );
            if (index % itemPreRow == 0)
                CardContents.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(item, index / itemPreRow);
            Grid.SetColumn(item, index % itemPreRow);
            item.Margin = new Thickness(0, 0, index % itemPreRow == itemPreRow - 1 ? 0 : 5, 5);
            CardContents.Children.Add(item);
            index++;
        }
    }
}