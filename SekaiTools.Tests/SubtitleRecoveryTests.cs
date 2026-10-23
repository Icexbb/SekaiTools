using SekaiToolsCore;
using SekaiToolsCore.Abstractions;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Persistence;

namespace SekaiTools.Tests;

public sealed class SubtitleRecoveryTests
{
    [Fact]
    public void RecoveryIsConsumedOnceIncludingDuplicateAndOlderLegacyFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            void Save(string relative, string video, ProcessStopReason reason = ProcessStopReason.Canceled) =>
                ProgressStore.SaveToPath(Path.Combine(root, relative), new ProcessingState { VideoFilePath = video, StopReason = reason });
            Save("SubtitleQueueProgress/shared.json", "queue.mp4");
            Save("SubtitleQueueProgress/second.json", "missing-materials.mp4");
            Save("Progress/shared.json", "legacy.mp4");
            Save("Progress/older.json", "older.mp4");
            File.SetLastWriteTimeUtc(Path.Combine(root, "Progress/older.json"), DateTime.UtcNow.AddHours(-1));
            Save("SubtitleQueueProgress/done.json", "completed.mp4", ProcessStopReason.Completed);
            var first = SubtitleRecoveryStore.TakeOnce(root);
            Assert.Equal(2, first.Count);
            Assert.Equal("queue.mp4", first.Single(entry => entry.SaveKey == "shared").State.VideoFilePath);
            Assert.Empty(SubtitleRecoveryStore.TakeOnce(root));
            Assert.DoesNotContain(HistoryCatalog.Read(root), entry => !entry.IsHistory);
            Assert.False(File.Exists(Path.Combine(root, "Progress/older.json")));
            Assert.True(File.Exists(Path.Combine(root, "SubtitleQueueProgress/done.json")));
            // An independently created task still gets a new recovery opportunity.
            Save("SubtitleQueueProgress/new.json", "new.mp4");
            Assert.Equal("new", Assert.Single(SubtitleRecoveryStore.TakeOnce(root)).SaveKey);
            Assert.Empty(SubtitleRecoveryStore.TakeOnce(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void LatestLegacyRecordIsReturnedOnlyOnceWithoutQueueRecords()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root,"Progress","legacy.json");
            ProgressStore.SaveToPath(path, new ProcessingState { VideoFilePath = "legacy.mp4", StopReason = ProcessStopReason.EndOfStream });
            Assert.Equal("legacy", Assert.Single(SubtitleRecoveryStore.TakeOnce(root)).SaveKey);
            Assert.False(File.Exists(path));
            Assert.Empty(SubtitleRecoveryStore.TakeOnce(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root,true); }
    }

    [Fact]
    public void RecoveredTaskCannotRecreateCheckpointButKeepsCompletedHistory()
    {
        var sink = new Sink();
        var recovered = new RecoveredTaskPersistence(sink);
        recovered.SaveProgress("task", new ProcessingState { StopReason = ProcessStopReason.Canceled });
        recovered.SaveProgress("task", new ProcessingState { StopReason = ProcessStopReason.EndOfStream });
        Assert.Equal(0, sink.Saves);
        recovered.DeleteProgress("task");
        Assert.Equal(1, sink.Deletes);
        recovered.AddHistory(new ProcessingState { StopReason = ProcessStopReason.Completed });
        Assert.Equal(1, sink.Histories);
        sink.SaveProgress("new-task", new ProcessingState());
        Assert.Equal(1, sink.Saves);
    }

    private sealed class Sink : IProcessingStatePersistence
    {
        public int Saves, Deletes, Histories;
        public void SaveProgress(string key, ProcessingState state) => Saves++;
        public void DeleteProgress(string key) => Deletes++;
        public void AddHistory(ProcessingState state) => Histories++;
    }
}
