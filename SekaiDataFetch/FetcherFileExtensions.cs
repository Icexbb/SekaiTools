namespace SekaiDataFetch;

public static class FetcherFileExtensions
{
    public static async Task FetchToFile(this Fetcher fetcher, string url, string filePath)
    {
        var content = await fetcher.Fetch(url);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(filePath, content);
    }
}
