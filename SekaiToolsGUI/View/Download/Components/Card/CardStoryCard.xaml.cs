using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiDataFetch.Item;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

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
        Loaded += (_, _) => _ = UpdateThumbnailAsync();
        Unloaded += (_, _) => ResetThumbnail();
    }

    public CardStoryCard(CardStorySet cardStorySet) : this()
    {
        CardStorySet = cardStorySet;
    }

    public CardStorySet? CardStorySet
    {
        get => (CardStorySet?)GetValue(CardStoryImplProperty);
        set => SetValue(CardStoryImplProperty, value);
    }

    private static void OnCardStoryImplChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CardStoryCard control) return;
        control.ResetThumbnail();
        if (e.NewValue is CardStorySet cardStoryImpl)
        {
            control.Initialize(cardStoryImpl);
            if (control.IsLoaded) _ = control.UpdateThumbnailAsync();
        }
    }
}

public partial class CardStoryCard
{
    private static readonly ILogger ArtworkLogger = SekaiToolsBase.Logger.CreateLogger(nameof(CardStoryCard));
    private static readonly SemaphoreSlim ThumbnailSlots = new(4);
    private static readonly Fetcher ThumbnailFetcher = new();
    private CancellationTokenSource? _thumbnailLifetime;
    private Task<BitmapSource?>? _normalThumbnail;
    private Task<BitmapSource?>? _trainedThumbnail;
    private bool _showTrained;

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

    private void ResetThumbnail()
    {
        _thumbnailLifetime?.Cancel();
        _thumbnailLifetime?.Dispose();
        _thumbnailLifetime = null;
        _normalThumbnail = null;
        _trainedThumbnail = null;
        _showTrained = false;
        ArtworkThumbnail.Source = null;
        TrainedArtworkOverlay.BeginAnimation(OpacityProperty, null);
        TrainedArtworkOverlay.Opacity = 0;
        TrainedArtworkOverlay.Source = null;
        AutomationProperties.SetName(ArtworkThumbnail, "");
    }

    private void ArtworkPreview_OnMouseEnter(object sender, MouseEventArgs e) => _ = UpdateThumbnailAsync();

    private void ArtworkPreview_OnMouseLeave(object sender, MouseEventArgs e) => _ = UpdateThumbnailAsync();

    private async Task UpdateThumbnailAsync()
    {
        if (!IsLoaded || CardStorySet?.Card is not { } card || ArtworkPreviewButton.Visibility != Visibility.Visible) return;
        var lifetime = _thumbnailLifetime ??= new CancellationTokenSource();
        var normal = await (_normalThumbnail ??= LoadThumbnailAsync(ArtworkUrls(card, false), lifetime.Token));
        if (_thumbnailLifetime != lifetime) return;
        var trained = ArtworkPreviewButton.IsMouseOver && CardArtwork.HasTrainedArtwork(card);
        ArtworkThumbnail.Source = normal;
        if (!trained)
        {
            TransitionArtwork(false);
            return;
        }
        if (TrainedArtworkOverlay.Source == null && normal != null)
            AutomationProperties.SetName(ArtworkThumbnail, "特训前卡图缩略图");
        var upgraded = await (_trainedThumbnail ??= LoadThumbnailAsync(ArtworkUrls(card, true), lifetime.Token));
        if (_thumbnailLifetime == lifetime && ArtworkPreviewButton.IsMouseOver && upgraded != null)
        {
            TrainedArtworkOverlay.Source = upgraded;
            TransitionArtwork(true);
        }
    }

    private void TransitionArtwork(bool trained)
    {
        var target = trained ? 1d : 0d;
        AutomationProperties.SetName(ArtworkThumbnail, trained ? "特训后卡图缩略图"
            : ArtworkThumbnail.Source != null ? "特训前卡图缩略图" : "");
        if (_showTrained == trained) return;
        _showTrained = trained;

        var current = TrainedArtworkOverlay.Opacity;
        if (!SystemParameters.ClientAreaAnimation)
        {
            TrainedArtworkOverlay.BeginAnimation(OpacityProperty, null);
            TrainedArtworkOverlay.Opacity = target;
            return;
        }

        TrainedArtworkOverlay.Opacity = current;
        TrainedArtworkOverlay.BeginAnimation(OpacityProperty,
            new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static async Task<BitmapSource?> LoadThumbnailAsync(IReadOnlyList<Uri> urls, CancellationToken token)
    {
        if (urls.Count == 0) return null;
        try
        {
            await ThumbnailSlots.WaitAsync(token);
            try
            {
                ThumbnailFetcher.SetProxy(SettingPageModel.Instance.GetProxy());
                return await DownloadSources.TryAsync(urls,
                    (uri, cancellation) => CardArtworkDialog.LoadAsync(ThumbnailFetcher, uri, cancellation, decodeWidth: 320), token);
            }
            finally
            {
                ThumbnailSlots.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error)
        {
            ArtworkLogger.LogDebug(error, "Card artwork thumbnail unavailable: {Url}", urls[0]);
            return null;
        }
    }

    private static Uri[] ArtworkUrls(SekaiToolsBase.DataList.Card card, bool trained) =>
        DownloadPageModel.Instance.OrderedSources.Select(source => CardArtwork.Locate(source, card, trained))
            .OfType<Uri>().Distinct().ToArray();

    private async void ArtworkPreview_OnClick(object sender, RoutedEventArgs e)
    {
        if (CardStorySet?.Card is not { } card || Window.GetWindow(this) is not MainWindow window) return;
        var normal = ArtworkUrls(card, false);
        if (normal.Length == 0) return;
        var trained = ArtworkUrls(card, true);
        var dialog = new CardArtworkDialog($"#{card.Id} {card.Prefix}", normal, trained, _showTrained);
        await dialog.ShowPreviewAsync(window.WindowContentDialogService);
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

        itemFirst.UseCompactLayout();
        itemSecond.UseCompactLayout();
        itemFirst.Margin = new Thickness(0, 0, 6, 0);
        itemSecond.Margin = new Thickness(0);

        PanelItems.Children.Add(itemFirst);
        PanelItems.Children.Add(itemSecond);
    }
}
