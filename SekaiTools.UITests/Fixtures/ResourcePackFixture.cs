using System.Security.Cryptography;
using System.Text.Json;

namespace SekaiTools.UITests.Fixtures;

public static class ResourcePackFixture
{
    public static string DirectoryFor(string kind) => Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_EXE"))!, "..", "fixtures", kind));

    public static void Serve(ApplicationFixture app, LoopbackHttpFixture server, string kind, string lockName)
        => Serve(app, server, (kind, lockName));

    public static void ServeWorkbench(ApplicationFixture app, LoopbackHttpFixture server)
    {
        Serve(app, server, ("videoProcess", "video-resources.lock.json"), ("vapourSynth", "media-resources.lock.json"));
        var serveResources = server.Response!;
        using var endpoints = JsonDocument.Parse(File.ReadAllText(Path.Combine(app.Data.Root, "network-endpoints.json")));
        var sources = endpoints.RootElement.GetProperty("defaultDataSources").GetRawText();
        server.Response = request => request.Contains("/sources.json ", StringComparison.Ordinal) ? sources
            : request.Contains("/master/", StringComparison.Ordinal) ? "[]"
            : serveResources(request);
    }

    private static void Serve(ApplicationFixture app, LoopbackHttpFixture server, params (string Kind, string LockName)[] packs)
    {
        var files = new Dictionary<string, byte[]>();
        var manifests = new Dictionary<string, string>();
        foreach (var (kind, lockName) in packs)
        {
            var pack = DirectoryFor(kind);
            var manifest = new List<object>();
            using var resourceLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", lockName)));
            foreach (var entry in resourceLock.RootElement.EnumerateArray())
            {
                var name = entry.GetProperty("Name").GetString()!;
                var path = Path.GetFullPath(Path.Combine(pack, name));
                if (!SekaiToolsConfiguration.ApplicationPaths.ContainsPath(pack, path)) throw new InvalidDataException("资源锁定路径越界。");
                var bytes = File.ReadAllBytes(path);
                if (Convert.ToHexString(SHA256.HashData(bytes)) != entry.GetProperty("Sha256").GetString())
                    throw new InvalidDataException("测试资源版本不匹配：" + name);
                files.Add($"/resources/{kind}/{name} ", bytes);
                manifest.Add(new { Path = kind + "/" + name, Size = bytes.Length, Md5 = Convert.ToHexString(MD5.HashData(bytes)) });
            }
            manifests.Add($"/resources/{kind}.json ", JsonSerializer.Serialize(manifest));
        }
        var config = Path.Combine(app.Data.Root, "network-endpoints.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
        server.Response = request => manifests.FirstOrDefault(manifest => request.Contains(manifest.Key, StringComparison.Ordinal)).Value;
        server.BinaryResponse = request => files.FirstOrDefault(file => request.Contains(file.Key, StringComparison.Ordinal)).Value;
    }
}
