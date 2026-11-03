using SekaiDataFetch.List;

namespace SekaiTools.Tests;

public class ListRefreshProgressTests
{
    [Fact]
    public void ProgressCountsOnlyDownloadedFiles()
    {
        var progress = new ListRefreshProgress(ListRefreshStage.Downloading,
            [new ListRefreshFileProgress("cards.json", true), new ListRefreshFileProgress("cardEpisodes.json", false)]);

        Assert.Equal(1, progress.CompletedFiles);
        Assert.Equal(50, progress.Percentage);
    }

    [Fact]
    public void EmptyProgressDoesNotDivideByZero()
    {
        var progress = new ListRefreshProgress(ListRefreshStage.Downloading, []);

        Assert.Equal(0, progress.CompletedFiles);
        Assert.Equal(0, progress.Percentage);
    }

    [Fact]
    public void UnavailableOptionalFilesFinishWithoutBeingCountedAsDownloaded()
    {
        var progress = new ListRefreshProgress(ListRefreshStage.Saving,
        [
            new ListRefreshFileProgress("actionSets.json", true),
            new ListRefreshFileProgress("releaseConditions.json", false, true)
        ]);
        Assert.Equal(1, progress.CompletedFiles);
        Assert.Equal(1, progress.UnavailableFiles);
        Assert.Equal(100, progress.Percentage);
    }
}