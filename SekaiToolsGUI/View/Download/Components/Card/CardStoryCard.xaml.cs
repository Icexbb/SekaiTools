using System.Windows;
using System.Windows.Controls;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download.Components.Card;

public partial class CardStoryCard : UserControl
{
    public static readonly DependencyProperty CardStoryImplProperty =
        DependencyProperty.Register(
            nameof(CardStorySet),
            typeof(CardStorySet),
            typeof(CardStoryCard),
            new PropertyMetadata(null, OnCardStoryImplChanged));

    public CardStoryCard()
    {
        InitializeComponent();
        Margin = new Thickness(5);
    }

    public CardStoryCard(CardStorySet cardStorySet)
    {
        InitializeComponent();
        Margin = new Thickness(5);

        CardStorySet = cardStorySet;
        Initialize(cardStorySet);
    }

    public CardStorySet? CardStorySet
    {
        get => (CardStorySet?)GetValue(CardStoryImplProperty);
        set => SetValue(CardStoryImplProperty, value);
    }

    private static void OnCardStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CardStoryCard control && e.NewValue is CardStorySet cardStoryImpl)
            control.Initialize(cardStoryImpl);
    }
}

public partial class CardStoryCard
{
    private string CardName { get; set; } = "";

    private void Initialize(CardStorySet cardStorySet)
    {
        CardStorySet = cardStorySet;
        var rarity = CardStorySet.Card.CardRarityType.Replace("rarity_", "") switch
        {
            "1" => "★ 1",
            "2" => "★ 2",
            "3" => "★ 3",
            "4" => "★ 4",
            "birthday" => "生日",
            _ => CardStorySet.Card.CardRarityType
        };

        CardName = $"No.{CardStorySet.Card.Id} {rarity} {CardStorySet.Card.Prefix}";
        TextBlockTitle.Text = CardName;

        InitDownloadItems();
    }

    private void InitDownloadItems()
    {
        if (CardStorySet == null) return;

        SourceList.Instance.SourceData = DownloadPageModel.Instance.CurrentSource.Data;

        foreach (var panelItemsChild in PanelItems.Children)
        {
            if (panelItemsChild is DownloadItem downloadItem) downloadItem.Recycle();
        }

        var itemFirst = DownloadItem.GetItem(() => SourceList.Instance.MemberStory(CardStorySet.FirstPart),
            CardName + " 前篇");
        var itemSecond = DownloadItem.GetItem(() => SourceList.Instance.MemberStory(CardStorySet.SecondPart),
            CardName + " 后篇");

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