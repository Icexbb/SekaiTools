using System.Text.Json;

namespace SekaiToolsHost;

public sealed record HostRequest(int Version, string Command, JsonElement Args);

public static class Protocol
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static HostRequest Parse(string line)
    {
        var request = JsonSerializer.Deserialize<HostRequest>(line, Json)
            ?? throw new InvalidDataException("请求为空");
        if (request.Version != 1) throw new InvalidDataException("不支持的协议版本");
        if (string.IsNullOrWhiteSpace(request.Command)) throw new InvalidDataException("缺少命令");
        return request;
    }

    public static T Arguments<T>(HostRequest request) => request.Args.Deserialize<T>(Json)
        ?? throw new InvalidDataException("缺少参数");

    public static void AtomicWrite(string path, string content)
    {
        path = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content, new System.Text.UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void RejectInputOverwrite(string output, params string[] inputs)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (inputs.Any(path => !string.IsNullOrEmpty(path) &&
            string.Equals(Path.GetFullPath(output), Path.GetFullPath(path), comparison)))
            throw new InvalidDataException("输出文件不能覆盖输入素材");
    }
}
