using System.Security.Cryptography;
using System.Text;

namespace SekaiDataFetch;

public static class ScenarioVoice
{
    public static Uri Locate(string storageBaseUrl, string scenarioId, string voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId)) throw new ArgumentException("语音标识为空");
        var root = new Uri(storageBaseUrl.TrimEnd('/') + "/");
        if (voiceId.StartsWith("partvoice_", StringComparison.Ordinal))
            return new Uri(root,
                "sound/scenario/part_voice/" + PartVoiceFolder(voiceId) + "/" + Uri.EscapeDataString(voiceId) + ".mp3");
        if (string.IsNullOrWhiteSpace(scenarioId)) throw new ArgumentException("剧本标识为空");
        return new Uri(root,
            "sound/scenario/voice/" + Uri.EscapeDataString(scenarioId) + "/" + Uri.EscapeDataString(voiceId) + ".mp3");
    }

    private static string PartVoiceFolder(string voiceId)
    {
        var parts = voiceId.Split('_');
        if (parts.Length < 3) throw new ArgumentException("无法识别短语音标识：" + voiceId);
        var character = parts[2] switch
        {
            "021" => "21miku",
            "022" => "22rin",
            "023" => "23len",
            "024" => "24luka",
            "025" => "25meiko",
            "026" => "26kaito",
            _ => throw new ArgumentException("无法识别短语音角色：" + voiceId)
        };
        var group = "piapro";
        if (parts[2] == "021" && parts.Length > 3)
            group = string.Join("_", parts.Skip(3)) switch
            {
                "piapro" => "piapro",
                "band" or "light_sound" => "light_sound",
                "idol" => "idol",
                "street" => "street",
                "wonder" or "theme_park" => "theme_park",
                "night" or "school_refusal" => "school_refusal",
                _ => throw new ArgumentException("无法识别短语音分组：" + voiceId)
            };
        return character + "_" + group;
    }

    public static async Task<string> LoadAsync(Fetcher fetcher, Uri uri, string cacheDirectory, CancellationToken token)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        token.ThrowIfCancellationRequested();
        var category = uri.AbsolutePath.Contains("/sound/scenario/part_voice/", StringComparison.Ordinal)
            ? "partvoice"
            : "voice";
        var directory = Path.Combine(cacheDirectory, category);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, key + ".mp3");
        var legacy = Path.Combine(cacheDirectory, key + ".mp3");
        if (!File.Exists(path) && File.Exists(legacy))
            try
            {
                File.Move(legacy, path);
            }
            catch (IOException) when (File.Exists(path))
            {
            }

        var limit = VoiceCache.AutomaticLimit(cacheDirectory);
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            VoiceCache.Trim(cacheDirectory, limit, path);
            return path;
        }

        var bytes = await fetcher.FetchBytesAsync(uri.AbsoluteUri, token).ConfigureAwait(false);
        if (bytes.Length == 0) throw new IOException("语音文件为空");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        VoiceCache.Trim(cacheDirectory, limit, path);
        return path;
    }
}