using SekaiDataFetch.List;

namespace SekaiTools.Tests;

public class ListRefreshProgressTests
{
    [Fact]
    public void ProgressCountsOnlyDownloadedFiles()
    {
        var progress = new ListRefreshProgress(ListRefreshStage.Downloading,
            [new("cards.json", true), new("cardEpisodes.json", false)]);

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
            [new("actionSets.json", true), new("releaseConditions.json", false, true)]);
        Assert.Equal(1, progress.CompletedFiles);
        Assert.Equal(1, progress.UnavailableFiles);
        Assert.Equal(100, progress.Percentage);
    }
}
