using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private double _split = 1;

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
        ArtworkThumbnail.Source = null;
        TrainedArtworkOverlay.Source = null;
        UpdateComparison(animate: false);
    }

    private void ArtworkPreview_OnPointerChanged(object sender, MouseEventArgs e) => UpdateComparison();

    private void ArtworkViewport_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateComparison(animate: false);

    private async Task UpdateThumbnailAsync()
    {
        if (!IsLoaded || CardStorySet?.Card is not { } card || ArtworkPreviewButton.Visibility != Visibility.Visible) return;
        if (_thumbnailLifetime != null) return;
        var lifetime = _thumbnailLifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        var trained = LoadThumbnailAsync(ArtworkUrls(card, true), token);
        var normal = await LoadThumbnailAsync(ArtworkUrls(card, false), token);
        if (_thumbnailLifetime != lifetime) return;
        ArtworkThumbnail.Source = normal;
        UpdateComparison(animate: false);
        var upgraded = await trained;
        if (_thumbnailLifetime != lifetime || upgraded == null) return;
        TrainedArtworkOverlay.Source = upgraded;
        UpdateComparison();
    }

    private double GetComparisonSplit()
    {
        if (TrainedArtworkOverlay.Source == null) return 1;
        if (ArtworkThumbnail.Source == null) return 0;
        if (!ArtworkPreviewButton.IsMouseOver) return 0.5;
        return Mouse.GetPosition(ArtworkViewport).X < ArtworkViewport.ActualWidth / 2 ? 1 : 0;
    }

    private void UpdateComparison(bool animate = true)
    {
        var split = GetComparisonSplit();
        AutomationProperties.SetName(ArtworkThumbnail, split switch
        {
            0 => "特训后卡图缩略图",
            0.5 => "并列卡图缩略图",
            _ => ArtworkThumbnail.Source != null ? "特训前卡图缩略图" : ""
        });
        ApplyComparisonSplit(split, animate);
    }

    private void ApplyComparisonSplit(double split, bool animate)
    {
        if (animate && _split == split) return;
        _split = split;

        var width = ArtworkViewport.ActualWidth;
        var target = new Rect(width * split, 0, width * (1 - split), ArtworkViewport.ActualHeight);
        if (animate && SystemParameters.ClientAreaAnimation && !TrainedArtworkClip.Rect.IsEmpty)
        {
            var current = TrainedArtworkClip.Rect;
            TrainedArtworkClip.Rect = current;
            TrainedArtworkClip.BeginAnimation(RectangleGeometry.RectProperty,
                new RectAnimation(current, target, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            return;
        }

        TrainedArtworkClip.BeginAnimation(RectangleGeometry.RectProperty, null);
        TrainedArtworkClip.Rect = target;
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
        var dialog = new CardArtworkDialog($"#{card.Id} {card.Prefix}", normal, trained, GetComparisonSplit() == 0);
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
