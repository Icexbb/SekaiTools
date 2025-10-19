using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Emgu.CV;
using Microsoft.Extensions.Logging;

namespace SekaiToolsCore;

public enum ResourceType
{
    VapourSynth,
    VideoProcess
}

public struct Resource
{
    // {
    //     "path": "vapourSynth/7z.dll",
    //     "size": 1892864,
    //     "md5": "1143c4905bba16d8cc02c6ba8f37f365"
    // }

    public string Path { get; set; }
    public string Md5 { get; set; }

    public long Size { get; set; }
}

public static class ResourceManager
{
    private const string ResourceServerUrl = "https://resource.g.xbb.moe/";

    public static readonly string DataBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools");

    private static readonly string BasePath = Path.Combine(DataBaseDir, "Resource");

    private static readonly Dictionary<ResourceType, string> ResourceTypePathMap = new()
    {
        { ResourceType.VapourSynth, "vapourSynth" },
        { ResourceType.VideoProcess, "videoProcess" }
    };

    public static string ResourcePath(ResourceType type, string fileName)
    {
        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var filename = Path.Combine(BasePath, typeDir, fileName);
        if (File.Exists(filename)) return filename;

        throw new FileNotFoundException($"{Path.Combine(BasePath, fileName)} not found");
    }

    public static bool CheckResource(ResourceType type)
    {
        var fileList = GetFileList(type);
        return fileList.All(file => CheckResourceFile(type, file));
    }

    private static bool CheckResourceFile(ResourceType type, Resource file)
    {
        var filename = NormalizePath(Path.Combine(BasePath, file.Path));
        if (!File.Exists(filename)) return false;
        return file.Size == new FileInfo(filename).Length &&
               string.Equals(file.Md5, CalculateMd5(filename), StringComparison.CurrentCultureIgnoreCase);
    }

    private static string CalculateMd5(string filename)
    {
        using var md5 = MD5.Create();
        // 使用 FileStream 打开文件，并传入到 ComputeHash 方法中
        using var stream = File.OpenRead(filename);
        // 计算哈希值
        var hashBytes = md5.ComputeHash(stream);

        // 将字节数组转换为十六进制字符串
        var sb = new StringBuilder();
        foreach (var t in hashBytes) sb.Append(t.ToString("X2"));

        return sb.ToString();
    }

    public static async Task EnsureResource(ResourceType type)
    {
        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var fileList = GetFileList(type);

        var tasks = fileList.Select<Resource, Task>(file => EnsureResourceFile(type, file)).ToArray();
        foreach (var task in tasks)
        {
            await task;
        }

        // delete files do not exist in the resource list
        // foreach (var file in Directory.GetFiles(Path.Combine(BasePath, typeDir)))
        // {
        //     if (fileList.Any(f =>
        //             NormalizePath(Path.Combine(BasePath, f.Path)) ==
        //             NormalizePath(Path.GetFileName(file)))) continue;
        //     File.Delete(file);
        // }

        return;
    }

    private static async Task EnsureResourceFile(ResourceType type, Resource resource)
    {
        var filename = NormalizePath(Path.Combine(BasePath, resource.Path));
        var fileDir = Path.GetDirectoryName(filename);
        if (fileDir != null && !Directory.Exists(fileDir)) Directory.CreateDirectory(fileDir);
        if (CheckResourceFile(type, resource)) return;

        if (File.Exists(filename)) File.Delete(filename);
        var fileUrl = ResourceServerUrl + resource.Path;

        Console.WriteLine($"Downloading {fileUrl}");
        var response = await Client.GetAsync(fileUrl);
        response.EnsureSuccessStatusCode();
        var fileBytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(filename, fileBytes);
        Console.WriteLine($"Download completed: {filename}");
    }

    private static HttpClient Client { get; } = new();

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static Resource[] GetFileList(ResourceType type)
    {
        if (ResourceFileList.TryGetValue(type, out var resources))
        {
            return resources;
        }

        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var fileListUrl = ResourceServerUrl + $"{typeDir}.json";

        Console.WriteLine($"Downloading {fileListUrl}");
        var response = Client.GetAsync(fileListUrl).Result;
        response.EnsureSuccessStatusCode();
        var fileListJson = response.Content.ReadAsStringAsync().Result;

        var fileList = JsonSerializer.Deserialize<Resource[]>(fileListJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
        ResourceFileList[type] = fileList;
        return fileList;
    }

    private static readonly Dictionary<ResourceType, Resource[]> ResourceFileList = new();
}