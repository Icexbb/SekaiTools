using System.IO;
using System.Security.Cryptography;
using System.Text;
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
                    (uri, cancellation) => LoadAsync(ThumbnailFetcher, uri, cancellation, cacheName, decodeWidth: 320), token);
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
        var directory = Path.Combine(ApplicationPaths.Current.DataRoot, "Data", cacheName);
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
                Logger.LogDebug(error, "Story image cache read failed: {Path}", path);
            }
        }

        Logger.LogInformation("Downloading story image: {Url}", uri);
        var bytes = await fetcher.FetchBytesAsync(uri.AbsoluteUri, token).ConfigureAwait(false);
        BitmapSource bitmap;
        try
        {
            bitmap = Decode(bytes, decodeWidth);
        }
        catch (Exception error) when (error is FileFormatException or NotSupportedException or IOException)
        {
            throw new InvalidDataException($"下载源返回的图片无效：{uri}", error);
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
