using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel.Download;

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
    }

    public CardStoryCard(CardStorySet cardStorySet)
    {
        InitializeComponent();

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

        TextBlockTitle.Text = $"# {CardStorySet.Card.Id}";
        TextBlockName.Text = $"{rarity} - {CardStorySet.Card.Prefix}";
        var url =
            $"pack://application:,,,/SekaiToolsGUI;component/Resource/Characters/chr_{cardStorySet.Card.CharacterId}.png";
        IconImage.Source = new BitmapImage(new Uri(url));
        ArtworkPreviewButton.Visibility = CardArtwork.Locate(
            DownloadPageModel.Instance.CurrentSource, cardStorySet.Card, trained: false) != null
            ? Visibility.Visible
            : Visibility.Collapsed;
        AutomationProperties.SetAutomationId(ArtworkPreviewButton, $"StoryDownload.Card.{cardStorySet.Card.Id}.PreviewArtwork");

        InitDownloadItems();
    }

    private async void ArtworkPreview_OnClick(object sender, RoutedEventArgs e)
    {
        if (CardStorySet?.Card is not { } card || Window.GetWindow(this) is not MainWindow window) return;
        var sources = DownloadPageModel.Instance.OrderedSources;
        var normal = ArtworkUrls(false);
        if (normal.Length == 0) return;
        var trained = ArtworkUrls(true);
        var dialog = new CardArtworkDialog($"#{card.Id} {card.Prefix}", normal, trained);
        await dialog.ShowPreviewAsync(window.WindowContentDialogService);

        Uri[] ArtworkUrls(bool afterTraining) => sources
            .Select(source => CardArtwork.Locate(source, card, afterTraining)).OfType<Uri>().Distinct().ToArray();
    }

    private void InitDownloadItems()
    {
        if (CardStorySet == null) return;


        foreach (var panelItemsChild in PanelItems.Children.OfType<DownloadItem>().ToList())
            panelItemsChild.Recycle();

        var itemFirst = DownloadItem.GetItem(
            source => new SourceList(source).MemberStory(CardStorySet.FirstPart),
            "前篇", "",
            $"CardStory|{CardStorySet.Card.Id}-{CardStorySet.Card.Prefix}|前篇"
        );
        var itemSecond = DownloadItem.GetItem(
            source => new SourceList(source).MemberStory(CardStorySet.SecondPart),
            "后篇", "",
            $"CardStory|{CardStorySet.Card.Id}-{CardStorySet.Card.Prefix}|后篇"
        );

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
