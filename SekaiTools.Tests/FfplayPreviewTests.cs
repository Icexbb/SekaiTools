using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class FfplayPreviewTests
{
    [Fact]
    public void DiscoverySkipsMissingDirectoriesAndAcceptsQuotedPaths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ffplay-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "ffplay.exe");
        File.WriteAllText(executable, "");
        try
        {
            Assert.Equal(executable, FfplayPreview.FindExecutable(["", Path.Combine(directory, "missing"), $"\"{directory}\""]));
            Assert.Null(FfplayPreview.FindExecutable([Path.Combine(directory, "missing")]));
        }
        finally
        {
            File.Delete(executable);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task CancelledPreviewDoesNotStartAProcess()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FfplayPreview.PlayAsync(
            "missing-player", "missing-video", TimeSpan.Zero, TimeSpan.FromSeconds(1), false,
            "preview", new CancellationToken(true)));
    }

    [Fact]
    public async Task InvalidRangeDoesNotStartAProcess()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FfplayPreview.PlayAsync(
            "missing-player", "missing-video", TimeSpan.Zero, TimeSpan.Zero, false,
            "preview", CancellationToken.None));
    }
}
