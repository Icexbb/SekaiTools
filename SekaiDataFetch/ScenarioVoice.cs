using System.Security.Cryptography;
using System.Text;

namespace SekaiDataFetch;

public static class ScenarioVoice
{
    public static Uri Locate(string storageBaseUrl, string scenarioId, string voiceId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(voiceId))
            throw new ArgumentException("剧本或语音标识为空");
        var root = new Uri(storageBaseUrl.TrimEnd('/') + "/");
        var folder = scenarioId + (root.Host.Equals("storage.sekai.best", StringComparison.OrdinalIgnoreCase) ? "_rip" : "");
        return new Uri(root, "sound/scenario/voice/" + Uri.EscapeDataString(folder) + "/" + Uri.EscapeDataString(voiceId) + ".mp3");
    }

    public static async Task<string> LoadAsync(Fetcher fetcher, Uri uri, string cacheDirectory, CancellationToken token)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var path = Path.Combine(cacheDirectory, key + ".mp3");
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;
        var bytes = await fetcher.FetchBytesAsync(uri.AbsoluteUri, token).ConfigureAwait(false);
        if (bytes.Length == 0) throw new IOException("语音文件为空");
        Directory.CreateDirectory(cacheDirectory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return path;
    }
}
