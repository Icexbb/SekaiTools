using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekaiToolsConfiguration;

namespace SekaiTools.UITests.Helpers;

public sealed class TestDataManager
{
    public TestDataManager(string testId)
    {
        var artifacts = Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_ARTIFACTS")
            ?? throw new InvalidOperationException("必须指定 SEKAI_TOOLS_UI_ARTIFACTS。");
        Directory = Path.Combine(Path.GetFullPath(artifacts), testId + "-" + Guid.NewGuid().ToString("N"));
        Root = Path.Combine(Directory, "sandbox");
        System.IO.Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, ApplicationPaths.TestRootMarker), ApplicationPaths.TestRootMarkerContent);
        Paths = ApplicationPaths.Create(Root);
        foreach (var path in new[] { Paths.DataRoot, Paths.LocalRoot, Temp, Path.Combine(Paths.DataRoot, "Data") })
            System.IO.Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(Paths.DataRoot, "Data", "last-module.json"), "\"TranslatePage\"");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "network-endpoints.json"),
            Path.Combine(Root, NetworkEndpoints.FileName));
        var production = ApplicationPaths.Create(null);
        ProtectedRoots = [production.DataRoot, production.LocalRoot];
        Before = Snapshot();
    }

    public string Directory { get; }
    public string Root { get; }
    public string Temp => Path.Combine(Root, "Temp");
    public ApplicationPaths Paths { get; }
    private string[] ProtectedRoots { get; }
    public string Before { get; }

    public string Snapshot()
    {
        var entries = ProtectedRoots.SelectMany(root => System.IO.Directory.Exists(root)
                ? System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                : [])
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(file => { var info = new FileInfo(file); return $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}"; });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', entries))));
    }

    public void CleanTemporaryFiles()
    {
        _ = ApplicationPaths.Create(Root);
        if (!ApplicationPaths.ContainsPath(Root, Temp)) throw new InvalidOperationException("临时目录越界。");
        if (System.IO.Directory.Exists(Temp)) System.IO.Directory.Delete(Temp, true);
    }

    public void WriteJson(string name, object value) => File.WriteAllText(Path.Combine(Directory, name + ".json"),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
