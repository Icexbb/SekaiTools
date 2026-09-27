namespace SekaiDataFetch.List;

public enum ListRefreshStage
{
    Downloading,
    Saving,
    Loading,
    Completed
}

public record ListRefreshFileProgress(string FileName, bool IsDownloaded, bool IsUnavailable = false);

public record ListRefreshProgress(ListRefreshStage Stage, IReadOnlyList<ListRefreshFileProgress> Files)
{
    public int CompletedFiles => Files.Count(file => file.IsDownloaded);
    public int UnavailableFiles => Files.Count(file => file.IsUnavailable);
    public double Percentage => Files.Count == 0 ? 0 : 100.0 * (CompletedFiles + UnavailableFiles) / Files.Count;
}
