using System.Reflection;
using System.Text.Json;

namespace SekaiToolsConfiguration;

public sealed class NetworkEndpoints
{
    public const string FileName = "network-endpoints.json";
    private const string EmbeddedResourceName = "SekaiToolsConfiguration.network-endpoints.json";
    private static readonly Lazy<NetworkEndpoints> CurrentValue = new(() => Load());

    public required GitHubEndpointOptions GitHub { get; init; }
    public required ResourceEndpointOptions Resources { get; init; }
    public required List<DataSourceEndpointOptions> DefaultDataSources { get; init; }

    public static NetworkEndpoints Current => CurrentValue.Value;
    public static string RepositoryUrl => Current.GitHub.RepositoryUrl;

    public static NetworkEndpoints Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(AppContext.BaseDirectory, FileName);
        var json = File.Exists(path) ? File.ReadAllText(path) : ReadEmbeddedDefault();
        var endpoints = JsonSerializer.Deserialize<NetworkEndpoints>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("网络端点配置为空");
        endpoints.Validate();
        return endpoints;
    }

    private static string ReadEmbeddedDefault()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedResourceName)
                           ?? throw new FileNotFoundException($"找不到嵌入配置 {EmbeddedResourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void Validate()
    {
        ValidateHttpUrl(GitHub.RepositoryUrl, nameof(GitHub.RepositoryUrl));
        ValidateHttpUrl(GitHub.LatestReleaseApiUrl, nameof(GitHub.LatestReleaseApiUrl));
        ValidateHttpUrl(GitHub.ReleaseDownloadBaseUrl, nameof(GitHub.ReleaseDownloadBaseUrl));
        ValidateHttpUrl(Resources.BaseUrl, nameof(Resources.BaseUrl));
        ValidateHttpUrl(Resources.SourceListUrl, nameof(Resources.SourceListUrl));
        RequireTrailingSlash(GitHub.ReleaseDownloadBaseUrl, nameof(GitHub.ReleaseDownloadBaseUrl));
        RequireTrailingSlash(Resources.BaseUrl, nameof(Resources.BaseUrl));

        if (DefaultDataSources.Count == 0)
            throw new InvalidDataException("网络端点配置必须包含至少一个默认数据源");
        foreach (var source in DefaultDataSources)
        {
            if (string.IsNullOrWhiteSpace(source.SourceName))
                throw new InvalidDataException("默认数据源名称不能为空");
            ValidateHttpUrl(source.SourceTemplate, $"{source.SourceName}.SourceTemplate");
            ValidateHttpUrl(source.StorageBaseUrl, $"{source.SourceName}.StorageBaseUrl");
            if (!source.SourceTemplate.Contains("{type}", StringComparison.Ordinal))
                throw new InvalidDataException($"{source.SourceName}.SourceTemplate 缺少 {{type}} 占位符");
            RequireTrailingSlash(source.StorageBaseUrl, $"{source.SourceName}.StorageBaseUrl");
            foreach (var template in source.AssetTemplates)
                if (string.IsNullOrWhiteSpace(template))
                    throw new InvalidDataException($"{source.SourceName} 的资源路径模板不能为空");
        }
    }

    private static void ValidateHttpUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"{name} 不是有效的 HTTP(S) URL: {value}");
    }

    private static void RequireTrailingSlash(string value, string name)
    {
        if (!value.EndsWith("/", StringComparison.Ordinal))
            throw new InvalidDataException($"{name} 必须以 / 结尾: {value}");
    }
}

public sealed class GitHubEndpointOptions
{
    public required string RepositoryUrl { get; init; }
    public required string LatestReleaseApiUrl { get; init; }
    public required string ReleaseDownloadBaseUrl { get; init; }
}

public sealed class ResourceEndpointOptions
{
    public required string BaseUrl { get; init; }
    public required string SourceListUrl { get; init; }
}

public sealed class DataSourceEndpointOptions
{
    public required string SourceName { get; init; }
    public required string SourceTemplate { get; init; }
    public required string StorageBaseUrl { get; init; }
    public required string ActionSetTemplate { get; init; }
    public required string MemberStoryTemplate { get; init; }
    public required string EventStoryTemplate { get; init; }
    public required string SpecialStoryTemplate { get; init; }
    public required string UnitStoryTemplate { get; init; }

    internal IEnumerable<string> AssetTemplates =>
    [
        ActionSetTemplate,
        MemberStoryTemplate,
        EventStoryTemplate,
        SpecialStoryTemplate,
        UnitStoryTemplate
    ];
}
