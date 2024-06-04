using Newtonsoft.Json.Linq;

namespace SekaiDataFetch;

public static class Utils
{
    public static TU Get<TU>(this JObject json, string key, TU defaultValue)
    {
        if (json.TryGetValue(key, out var value)) return value.ToObject<TU>() ?? defaultValue;
        return defaultValue;
    }
}