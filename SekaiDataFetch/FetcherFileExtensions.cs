namespace SekaiDataFetch;

public static class FetcherFileExtensions
{
    public static async Task FetchToFile(this Fetcher fetcher, string url, string filePath,
        CancellationToken cancellationToken = default, bool overwrite = true)
    {
        var content = await fetcher.Fetch(url, cancellationToken).ConfigureAwait(false);
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