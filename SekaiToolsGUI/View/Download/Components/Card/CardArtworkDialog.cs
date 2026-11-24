using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsConfiguration;
using SekaiToolsGUI.Control;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SekaiToolsGUI.View.Download.Components.Card;

internal sealed class CardArtworkDialog : ContentDialog
{
    private static readonly ILogger Logger = SekaiToolsBase.Logger.CreateLogger(nameof(CardArtworkDialog));
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _status = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextWrapping = TextWrapping.Wrap
    };
    private CancellationTokenSource? _request;

    public CardArtworkDialog(string title, IReadOnlyList<Uri> normal, IReadOnlyList<Uri> trained, bool showTrained)
    {
        SetResourceReference(StyleProperty, typeof(ContentDialog));
        AdaptiveLayout.SetDialogPurpose(this, DialogPurpose.Workspace);
        Title = title;
        CloseButtonText = "关闭";
        DefaultButton = ContentDialogButton.Close;
        AutomationProperties.SetAutomationId(this, "StoryDownload.CardArtwork.Dialog");
        AutomationProperties.SetAutomationId(_status, "StoryDownload.CardArtwork.Status");
        AutomationProperties.SetAutomationId(_image, "StoryDownload.CardArtwork.Image");
        var panel = new Grid { Height = 440 };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition());
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        AddButton("特训前", "Normal", normal);
        if (trained.Count > 0) AddButton("特训后", "Trained", trained);
        panel.Children.Add(buttons);
        var viewer = new Grid();
        Grid.SetRow(viewer, 1);
        viewer.Children.Add(_image);
        viewer.Children.Add(_status);
        panel.Children.Add(viewer);
        Content = panel;
        Loaded += (_, _) => _ = showTrained
            ? ShowArtworkAsync(trained, "特训后")
            : ShowArtworkAsync(normal, "特训前");

        void AddButton(string label, string id, IReadOnlyList<Uri> urls)
        {
            var button = new Wpf.Ui.Controls.Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
            AutomationProperties.SetAutomationId(button, $"StoryDownload.CardArtwork.{id}");
            button.Click += async (_, _) => await ShowArtworkAsync(urls, label);
            buttons.Children.Add(button);
        }
    }

    public async Task ShowPreviewAsync(IContentDialogService service)
    {
        try
        {
            await service.ShowAsync(this, CancellationToken.None);
        }
        finally
        {
            _request?.Cancel();
            _request = null;
        }
    }

    private async Task ShowArtworkAsync(IReadOnlyList<Uri> urls, string label)
    {
        _request?.Cancel();
        using var request = new CancellationTokenSource();
        _request = request;
        _image.Source = null;
        _status.Text = $"正在加载{label}卡图…";
        try
        {
            using var fetcher = new Fetcher();
            fetcher.SetProxy(SettingPageModel.Instance.GetProxy());
            var bitmap = await DownloadSources.TryAsync(urls, (uri, token) => LoadAsync(fetcher, uri, token),
                request.Token);
            if (_request != request) return;
            _image.Source = bitmap;
            AutomationProperties.SetName(_image, $"{label}卡图");
            _status.Text = "";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Logger.LogWarning(error, "Card artwork preview failed: {Urls}", string.Join(", ", urls));
            if (_request == request) _status.Text = $"无法加载{label}卡图。点击上方按钮重试。";
        }
        finally
        {
            if (_request == request) _request = null;
        }
    }

    internal static async Task<BitmapSource> LoadAsync(Fetcher fetcher, Uri uri, CancellationToken token, int decodeWidth = 0)
    {
        var directory = Path.Combine(ApplicationPaths.Current.DataRoot, "Data", "CardArtworkCache");
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var path = Path.Combine(directory, key + ".png");
        if (File.Exists(path))
        {
            try
            {
                return Decode(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false), decodeWidth);
            }
            catch (Exception error) when (error is IOException or FileFormatException or NotSupportedException)
            {
                Logger.LogDebug(error, "Card artwork cache read failed: {Path}", path);
            }
        }

        Logger.LogInformation("Downloading card artwork: {Url}", uri);
        var bytes = await fetcher.FetchBytesAsync(uri.AbsoluteUri, token).ConfigureAwait(false);
        BitmapSource bitmap;
        try
        {
            bitmap = Decode(bytes, decodeWidth);
        }
        catch (Exception error) when (error is FileFormatException or NotSupportedException or IOException)
        {
            throw new InvalidDataException($"下载源返回的卡图无效：{uri}", error);
        }
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
        return bitmap;
    }

    private static BitmapSource Decode(byte[] bytes, int decodeWidth)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.DecodePixelWidth = decodeWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
