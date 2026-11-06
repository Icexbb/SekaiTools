using System.Text.Json;
using SekaiToolsCore.Process.Config;

namespace SekaiToolsInfrastructure.Development;

public static class DeveloperThresholds
{
    /// <summary>Validate the entire patch before returning a replacement snapshot.</summary>
    public static MatchingThreshold Patch(MatchingThreshold current, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object) throw new ArgumentException("阈值必须是 JSON 对象");
        var values = typeof(MatchingThreshold).GetProperties()
            .ToDictionary(p => p.Name, p => (double)p.GetValue(current)!);
        foreach (var item in patch.EnumerateObject())
        {
            if (!values.ContainsKey(item.Name)) throw new ArgumentException($"未知阈值：{item.Name}");
            if (item.Value.ValueKind != JsonValueKind.Number || !item.Value.TryGetDouble(out var value) || !double.IsFinite(value) || value <= 0 || value > 1)
                throw new ArgumentException($"{item.Name} 必须大于 0 且不超过 1");
            values[item.Name] = value;
        }

        return JsonSerializer.Deserialize<MatchingThreshold>(JsonSerializer.Serialize(values));
    }
}
