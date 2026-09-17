using System.Net;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekaiToolsBase;
using SekaiToolsConfiguration;
using SekaiToolsCore.Abstractions;
using SekaiToolsMedia;

namespace SekaiToolsInfrastructure.Resources;

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

public class ResourceManager : ITemplateResourceProvider, IMediaResourceProvider
{
    public static readonly string DataBaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools");

    private static readonly string BasePath = Path.Combine(DataBaseDir, "Resource");

    private static readonly Dictionary<ResourceType, string> ResourceTypePathMap = new()
    {
        { ResourceType.VapourSynth, "vapourSynth" },
        { ResourceType.VideoProcess, "videoProcess" }
    };

    private static readonly ConcurrentDictionary<ResourceType, Resource[]> ResourceFileList = new();
    private static readonly ConcurrentDictionary<ResourceType, SemaphoreSlim> ResourceLocks = new();
    private const int MaxConcurrentDownloads = 3;
    private static string ResourceServerUrl => NetworkEndpoints.Current.Resources.BaseUrl;

    public static ResourceManager Instance { get; } = new();

    private Proxy UserProxy { get; set; } = Proxy.None;

    public string GetVapourSynthResourcePath(string fileName)
    {
        return ResourcePath(ResourceType.VapourSynth, fileName);
    }

    public string GetVideoProcessResourcePath(string fileName)
    {
        return ResourcePath(ResourceType.VideoProcess, fileName);
    }

    public void SetProxy(Proxy proxy)
    {
        UserProxy = proxy;
    }

    private HttpMessageHandler GetHttpHandler()
    {
        return UserProxy.ProxyType switch
        {
            Proxy.Type.None => new HttpClientHandler(),
            Proxy.Type.System => new HttpClientHandler(),
            Proxy.Type.Http => new HttpClientHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            Proxy.Type.Socks5 => new SocketsHttpHandler
            {
                Proxy = new WebProxy(UserProxy.Host, UserProxy.Port), UseProxy = true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private async Task<HttpResponseMessage> Download(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient(GetHttpHandler())
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public string ResourcePath(ResourceType type, string fileName)
    {
        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var filename = EnsurePathWithinType(type, Path.Combine(BasePath, typeDir, fileName));
        return File.Exists(filename) ? filename : throw new FileNotFoundException($"{filename} not found");
    }

    public async Task<bool> CheckResource(ResourceType type, CancellationToken cancellationToken = default)
    {
        var fileList = await GetFileList(type, cancellationToken);
        return await Task.Run(() => fileList.All(file =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CheckResourceFile(type, file);
        }), cancellationToken);
    }

    private static bool CheckResourceFile(ResourceType type, Resource file)
    {
        var filename = EnsurePathWithinType(type, Path.Combine(BasePath, file.Path));
        return IsResourceValid(filename, file);
    }

    public static bool IsResourceValid(string filename, Resource resource)
    {
        try
        {
            return File.Exists(filename) &&
                   resource.Size == new FileInfo(filename).Length &&
                   string.Equals(resource.Md5, CalculateMd5(filename), StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
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

    public async Task EnsureResource(ResourceType type, CancellationToken cancellationToken = default)
    {
        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var resourceLock = ResourceLocks.GetOrAdd(type, static _ => new SemaphoreSlim(1, 1));
        await resourceLock.WaitAsync(cancellationToken);
        try
        {
            var fileList = await GetFileList(type, cancellationToken);
            using var downloadLimiter = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
            var tasks = fileList.Select(file => EnsureResourceFile(type, file, downloadLimiter, cancellationToken)).ToArray();
            await Task.WhenAll(tasks);
        }
        finally
        {
            resourceLock.Release();
        }

        // delete files do not exist in the resource list
        // foreach (var file in Directory.GetFiles(Path.Combine(BasePath, typeDir)))
        // {
        //     if (fileList.Any(f =>
        //             NormalizePath(Path.Combine(BasePath, f.Path)) ==
        //             NormalizePath(Path.GetFileName(file)))) continue;
        //     File.Delete(file);
        // }
    }

    private async Task EnsureResourceFile(
        ResourceType type,
        Resource resource,
        SemaphoreSlim downloadLimiter,
        CancellationToken cancellationToken)
    {
        var filename = EnsurePathWithinType(type, Path.Combine(BasePath, resource.Path));
        var fileDir = Path.GetDirectoryName(filename);
        if (fileDir != null && !Directory.Exists(fileDir)) Directory.CreateDirectory(fileDir);
        if (await Task.Run(() => CheckResourceFile(type, resource), cancellationToken)) return;

        await downloadLimiter.WaitAsync(cancellationToken);
        var temporaryFilename = filename + $".{Guid.NewGuid():N}.tmp";
        try
        {
            if (await Task.Run(() => CheckResourceFile(type, resource), cancellationToken)) return;
        var fileUrl = ResourceServerUrl + resource.Path;

        Console.WriteLine($"Downloading {fileUrl}");
            using var response = await Download(fileUrl, cancellationToken);
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(temporaryFilename, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            if (!IsResourceValid(temporaryFilename, resource))
                throw new InvalidDataException($"资源校验失败: {resource.Path}");

            File.Move(temporaryFilename, filename, true);
            Console.WriteLine($"Download completed: {filename}");
        }
        finally
        {
            downloadLimiter.Release();
            try
            {
                if (File.Exists(temporaryFilename)) File.Delete(temporaryFilename);
            }
            catch (IOException)
            {
                // 清理失败不覆盖下载或取消的原始错误。
            }
        }
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string EnsurePathWithinType(ResourceType type, string path)
    {
        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var typeRoot = NormalizePath(Path.Combine(BasePath, typeDir));
        var candidate = NormalizePath(path);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = typeRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, comparison))
            throw new InvalidDataException($"资源路径超出 {typeDir} 目录: {path}");

        return candidate;
    }

    private async Task<Resource[]> GetFileList(ResourceType type, CancellationToken cancellationToken = default)
    {
        if (ResourceFileList.TryGetValue(type, out var resources)) return resources;

        if (!ResourceTypePathMap.TryGetValue(type, out var typeDir))
            throw new ArgumentException($"ResourceType {type} not mapped");

        var fileListUrl = ResourceServerUrl + $"{typeDir}.json";

        Console.WriteLine($"Downloading {fileListUrl}");

        using var response = await Download(fileListUrl, cancellationToken);
        var fileListJson = await response.Content.ReadAsStringAsync(cancellationToken);

        var fileList = JsonSerializer.Deserialize<Resource[]>(fileListJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
        ResourceFileList[type] = fileList;
        return fileList;
    }
}
