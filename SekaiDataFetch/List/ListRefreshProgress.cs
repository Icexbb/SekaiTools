namespace SekaiDataFetch.List;

public enum ListRefreshStage
{
    Downloading,
    Saving,
    Loading,
    Completed
}

public record ListRefreshFileProgress(string FileName, bool IsDownloaded);

public record ListRefreshProgress(ListRefreshStage Stage, IReadOnlyList<ListRefreshFileProgress> Files)
{
    public int CompletedFiles => Files.Count(file => file.IsDownloaded);
    public double Percentage => Files.Count == 0 ? 0 : 100.0 * CompletedFiles / Files.Count;
}
