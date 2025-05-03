using System.Text.Json;

namespace SekaiDataFetch;

public static class Utils
{
    public static void Set<TKey, TValue>(this Dictionary<TKey, TValue> data, TKey key, TValue value)
        where TKey : notnull
    {
        data[key] = value;
    }

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}