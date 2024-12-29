using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch;
using SekaiDataFetch.List;

namespace SekaiToolsGUI.View.Download.Components.Card;

public partial class CardStoryCard : UserControl
{
    public static readonly DependencyProperty CardStoryImplProperty =
        DependencyProperty.Register(
            nameof(CardStoryImpl),
            typeof(CardStoryImpl),
            typeof(CardStoryCard),
            new PropertyMetadata(null, OnCardStoryImplChanged));

    public CardStoryCard()
    {
        InitializeComponent();
        Margin = new Thickness(5);
    }

    public CardStoryCard(CardStoryImpl cardStoryImpl, SourceType sourceType = SourceType.SiteBest)
    {
        InitializeComponent();
        Margin = new Thickness(5);

        CardStoryImpl = cardStoryImpl;
        Initialize(cardStoryImpl, sourceType);
    }

    public CardStoryImpl? CardStoryImpl
    {
        get => (CardStoryImpl?)GetValue(CardStoryImplProperty);
        set => SetValue(CardStoryImplProperty, value);
    }

    private static void OnCardStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CardStoryCard control && e.NewValue is CardStoryImpl cardStoryImpl)
            control.Initialize(cardStoryImpl);
    }
}

public partial class CardStoryCard
{
    private void Initialize(CardStoryImpl cardStoryImpl,
        SourceType sourceType = SourceType.SiteBest)
    {
        CardStoryImpl = cardStoryImpl;
        var rarity = CardStoryImpl.Card.CardRarityType.Replace("rarity_", "") switch
        {
            "1" => "★ 1",
            "2" => "★ 2",
            "3" => "★ 3",
            "4" => "★ 4",
            "birthday" => "生日",
            _ => CardStoryImpl.Card.CardRarityType
        };


        TextBlockTitle.Text = $"No.{CardStoryImpl.Card.Id} {rarity} {CardStoryImpl.Card.Prefix}";

        InitDownloadItems();
    }

    private void InitDownloadItems(SourceType sourceType = SourceType.SiteBest)
    {
        if (CardStoryImpl == null) return;
        var urlFirst = CardStoryImpl.Url(CardEpisodeType.FirstPart, sourceType);
        var urlSecond = CardStoryImpl.Url(CardEpisodeType.SecondPart, sourceType);

        foreach (var panelItemsChild in PanelItems.Children)
        {
            DownloadItem.RecycleItem((DownloadItem)panelItemsChild);
        }

        var itemFirst = DownloadItem.GetItem(urlFirst, "前篇");
        var itemSecond = DownloadItem.GetItem(urlSecond, "后篇");

        itemFirst.HorizontalAlignment = HorizontalAlignment.Stretch;
        itemSecond.HorizontalAlignment = HorizontalAlignment.Stretch;

        itemFirst.Margin = new Thickness(5);
        itemSecond.Margin = new Thickness(5);

        Grid.SetColumn(itemFirst, 0);
        Grid.SetColumn(itemSecond, 1);

        PanelItems.Children.Add(itemFirst);
        PanelItems.Children.Add(itemSecond);
    }
}