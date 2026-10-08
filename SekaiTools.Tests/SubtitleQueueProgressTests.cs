using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Persistence;

namespace SekaiTools.Tests;

public sealed class SubtitleQueueProgressTests
{
    [Fact]
    public void EnumerationPreservesAllIncompleteCheckpointsAndSkipsInvalidAndCompletedFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SekaiTools-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ProgressStore.SaveToPath(Path.Combine(directory, "canceled.json"), new ProcessingState
                { StopReason = ProcessStopReason.Canceled, VideoFilePath = "canceled.mp4" });
            ProgressStore.SaveToPath(Path.Combine(directory, "partial.json"), new ProcessingState
                { StopReason = ProcessStopReason.EndOfStream, VideoFilePath = "partial.mp4" });
            ProgressStore.SaveToPath(Path.Combine(directory, "finished.json"), new ProcessingState
                { StopReason = ProcessStopReason.Completed });
            File.WriteAllText(Path.Combine(directory, "invalid.json"), "broken JSON");
            var entries = SubtitleQueueProgressStore.EnumerateProgressFiles(directory);
            Assert.Equal(new[] { "canceled", "partial" }, entries.Select(entry => entry.SaveKey).Order());
            Assert.Equal(4, Directory.GetFiles(directory).Length);
        }
        finally { Directory.Delete(directory, true); }
    }
}
