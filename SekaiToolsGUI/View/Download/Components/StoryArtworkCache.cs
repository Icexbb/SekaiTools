using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using SekaiDataFetch;
using SekaiDataFetch.Source;
using SekaiToolsConfiguration;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components;

internal static class StoryArtworkCache
{
    private static readonly ILogger Logger = SekaiToolsBase.Logger.CreateLogger(nameof(StoryArtworkCache));
    private static readonly SemaphoreSlim ThumbnailSlots = new(4);
    private static readonly Fetcher ThumbnailFetcher = new();
    private const int ThumbnailWidth = 320;
    private const int CardPreviewWidth = 768;

    internal static async Task<BitmapSource?> LoadThumbnailAsync(IReadOnlyList<Uri> urls, CancellationToken token, string cacheName)
    {
        if (urls.Count == 0) return null;
        try
        {
            await ThumbnailSlots.WaitAsync(token);
            try
            {
                ThumbnailFetcher.SetProxy(SettingPageModel.Instance.GetProxy());
                return await DownloadSources.TryAsync(urls,
                    (uri, cancellation) => LoadAsync(ThumbnailFetcher, uri, cancellation, cacheName, ThumbnailWidth), token);
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
            Logger.LogDebug(error, "Story thumbnail unavailable: {Url}", urls[0]);
            return null;
        }
    }

    internal static async Task<BitmapSource> LoadAsync(Fetcher fetcher, Uri uri, CancellationToken token, string cacheName, int decodeWidth = 0)
    {
        var cacheWidth = cacheName == "EventBannerCache" ? ThumbnailWidth : CardPreviewWidth;
        var directory = Path.Combine(ApplicationPaths.Current.DataRoot, "Data", cacheName);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var path = Path.Combine(directory, key + ".png");
        if (File.Exists(path))
        {
            try
            {
                var cached = Decode(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false));
                if (cached.PixelWidth > cacheWidth)
                {
                    cached = Resize(cached, cacheWidth);
                    await WriteCacheAsync(cached).ConfigureAwait(false);
                }
                return Resize(cached, decodeWidth);
            }
            catch (Exception error) when (error is IOException or FileFormatException or NotSupportedException)
            {
                Logger.LogDebug(error, "Story image cache read failed: {Path}", path);
            }
        }

        Logger.LogInformation("Downloading story image: {Url}", uri);
        var bytes = await fetcher.FetchBytesAsync(uri.AbsoluteUri, token).ConfigureAwait(false);
        BitmapSource bitmap;
        try
        {
            bitmap = Resize(Decode(bytes), cacheWidth);
        }
        catch (Exception error) when (error is FileFormatException or NotSupportedException or IOException)
        {
            throw new InvalidDataException($"下载源返回的图片无效：{uri}", error);
        }
        await WriteCacheAsync(bitmap).ConfigureAwait(false);
        return Resize(bitmap, decodeWidth);

        async Task WriteCacheAsync(BitmapSource image)
        {
            token.ThrowIfCancellationRequested();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var encoded = new MemoryStream();
            encoder.Save(encoded);

            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, key + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllBytesAsync(temporary, encoded.ToArray(), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }

    private static BitmapSource Resize(BitmapSource image, int maxWidth)
    {
        if (maxWidth <= 0 || image.PixelWidth <= maxWidth) return image;
        var scale = (double)maxWidth / image.PixelWidth;
        var resized = new TransformedBitmap(image, new ScaleTransform(scale, scale));
        resized.Freeze();
        return resized;
    }

    private static BitmapSource Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
