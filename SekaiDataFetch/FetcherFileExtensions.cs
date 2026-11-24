namespace SekaiDataFetch;

public static class FetcherFileExtensions
{
    public static async Task FetchToFile(this Fetcher fetcher, string url, string filePath,
        CancellationToken cancellationToken = default, bool overwrite = true)
    {
        var content = await fetcher.Fetch(url, cancellationToken).ConfigureAwait(false);
        await SaveDownloadAsync(content, filePath, cancellationToken, overwrite).ConfigureAwait(false);
    }

    internal static async Task SaveDownloadAsync(string content, string filePath, CancellationToken cancellationToken,
        bool overwrite)
    {
        var destination = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
