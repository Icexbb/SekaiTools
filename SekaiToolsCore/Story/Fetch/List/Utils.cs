namespace SekaiToolsCore.Story.Fetch.List;

public static class Utils
{
    public static void Set<TKey, TValue>(this Dictionary<TKey, TValue> data, TKey key, TValue value)
        where TKey : notnull
    {
        data[key] = value;
    }

    public static TValue? Get<TKey, TValue>(this Dictionary<TKey, TValue> data, TKey key)
        where TKey : notnull
    {
        return data.GetValueOrDefault(key);
    }

    public static void Set<TPrimaryKey, TSubKey, TValue>(this Dictionary<TPrimaryKey, Dictionary<TSubKey, TValue>> data,
        TPrimaryKey primaryKey, TSubKey subKey, TValue value)
        where TPrimaryKey : notnull
        where TSubKey : notnull
    {
        if (!data.TryGetValue(primaryKey, out var added))
        {
            added = new Dictionary<TSubKey, TValue>();
        }

        added[subKey] = value;
        data[primaryKey] = added;
    }

    public static TValue? Get<TPrimaryKey, TSubKey, TValue>(
        this Dictionary<TPrimaryKey, Dictionary<TSubKey, TValue>> data,
        TPrimaryKey primaryKey, TSubKey subKey)
        where TPrimaryKey : notnull
        where TSubKey : notnull
    {
        return data.TryGetValue(primaryKey, out var value) ? value.Get(subKey) : default;
    }
}