using System.Text.Json;
using SekaiToolsCore.Process;
using SekaiToolsInfrastructure.Persistence;

namespace SekaiTools.Tests;

public class IncrementalProgressStoreTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void AdaptiveSelectionHonorsExactSerializedByteBoundary(int delta)
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-inline-limit-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            var overhead = JsonSerializer.SerializeToUtf8Bytes(new ProcessingState()).Length;
            var state = new ProcessingState { VideoFilePath = new string('x', IncrementalProgressStore.InlineLimitBytes + delta - overhead) };
            Assert.Equal(IncrementalProgressStore.InlineLimitBytes + delta, JsonSerializer.SerializeToUtf8Bytes(state).Length);
            IncrementalProgressStore.Save(path, state);
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            Assert.Equal(delta > 0, document.RootElement.TryGetProperty("CheckpointStorage", out _));
            Assert.Equal(state.VideoFilePath, IncrementalProgressStore.Load(path).VideoFilePath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(40000)]
    [InlineData(60000)]
    public void AdaptiveStorageUsesFlatFrozenTimecodesAndSwitchesFormatsWithoutLosingState(int frameCount)
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-adaptive-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            var times = Enumerable.Range(0, 5000).Select(i => i * 33 + i / 10).ToArray();
            var small = new ProcessingState { CheckpointTimecodeBlocks = times.Chunk(4096).ToArray(), FrameIndex = 5000 };
            IncrementalProgressStore.Save(path, small);
            Assert.False(Directory.Exists(path + ".chunks"));
            Assert.Equal(times, IncrementalProgressStore.Load(path).Timecodes);
            Assert.Equal(times, JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(path))!.Timecodes);
            var done = new DialogFrameSetDto
            {
                Finished = true,
                Frames = Enumerable.Range(0, frameCount).Select(i => new FrameResultDto(i, 1234, 2345)).ToList()
            };
            var large = new ProcessingState { ReusesCompletedFrames = true, Dialog = new() { FrameSets = [done] } };
            IncrementalProgressStore.Save(path, large);
            Assert.True(Directory.Exists(path + ".chunks"));
            Assert.Equal(done.Frames, IncrementalProgressStore.Load(path).Dialog!.FrameSets[0].Frames);
            var files = Directory.GetFiles(path + ".chunks").ToDictionary(p => p, File.GetLastWriteTimeUtc);
            IncrementalProgressStore.Save(path, large);
            Assert.All(files, item => Assert.Equal(item.Value, File.GetLastWriteTimeUtc(item.Key)));
            IncrementalProgressStore.Save(path, small);
            Assert.Equal(times, IncrementalProgressStore.Load(path).Timecodes);
            // Keep prior generation blocks until task cleanup; an already-open reader may still need them.
            Assert.All(files, item => Assert.True(File.Exists(item.Key)));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.NotNull(Record.Exception(() => IncrementalProgressStore.Save(path, large)));
                Assert.NotNull(Record.Exception(() => IncrementalProgressStore.Save(path, new ProcessingState { FrameIndex = 123 })));
            }
            Assert.Equal(times, IncrementalProgressStore.Load(path).Timecodes);
            IncrementalProgressStore.Delete(path);
            Assert.False(Directory.Exists(path + ".chunks"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TimecodeBlocksReusePrefixAndRestoreFlatTimesIncludingVariableIntervals()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-timecodes-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            var rate = new SekaiToolsCore.Process.Model.FrameRate(30);
            rate.RestoreTimecodes(Enumerable.Range(0, 5000).Select(i => i * 40 + i / 10));
            var blocks = rate.CaptureTimecodeBlocks();
            IncrementalProgressStore.SaveChunked(path, new ProcessingState { ReusesCompletedFrames = true, CheckpointTimecodeBlocks = blocks });
            var stamp = Directory.GetFiles(path + ".chunks").ToDictionary(p => p, File.GetLastWriteTimeUtc);
            rate.RecordTimecode(5000, 200501);
            IncrementalProgressStore.SaveChunked(path, new ProcessingState { ReusesCompletedFrames = true, CheckpointTimecodeBlocks = rate.CaptureTimecodeBlocks() });
            Assert.Equal(rate.ExportTimecodes(), IncrementalProgressStore.Load(path).Timecodes);
            Assert.True(stamp.Count(p => File.GetLastWriteTimeUtc(p.Key) == p.Value) == stamp.Count);
            Assert.True(new FileInfo(path).Length < 2000);
            IncrementalProgressStore.Delete(path);
            Assert.False(Directory.Exists(path + ".chunks"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CatalogLoadsChunksOnDemandAndOneTimeRecoveryRemovesOwnedBlocks()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-chunks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "SubtitleQueueProgress", "task.json");
        try
        {
            IncrementalProgressStore.SaveChunked(path, new ProcessingState
            {
                VideoFilePath = "video.mp4",
                Banner = new() { FrameSets = [new() { Finished = true, Start = 2, End = 8 }] }
            });
            var entry = Assert.Single(HistoryCatalog.Read(root));
            Assert.Equal("video.mp4", entry.Summary.VideoFilePath);
            Assert.Equal(8, entry.LoadState().Banner!.FrameSets[0].End);
            var recovered = Assert.Single(SubtitleRecoveryStore.TakeOnce(root));
            Assert.Equal(2, recovered.State.Banner!.FrameSets[0].Start);
            Assert.False(Directory.Exists(path + ".chunks"));
            Assert.Empty(SubtitleRecoveryStore.TakeOnce(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void FailedManifestReplacementLeavesPreviousGenerationRecoverable()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-chunks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            IncrementalProgressStore.SaveChunked(path, new ProcessingState { FrameIndex = 10 });
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failure = Record.Exception(() => IncrementalProgressStore.SaveChunked(path, new ProcessingState
                { FrameIndex = 20, Dialog = new() { FrameSets = [new() { Finished = true, Frames = [new(3, 4, 5)] }] } }));
                Assert.True(failure is IOException or UnauthorizedAccessException);
            }
            Assert.Equal(10, IncrementalProgressStore.Load(path).FrameIndex);
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CompletedChunksAreReusedWhileActiveFramesAndEditedFullSnapshotsStayCurrent()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-chunks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            var done = new DialogFrameSetDto
            {
                Finished = true,
                Frames = Enumerable.Range(1, 1000).Select(i => new FrameResultDto(i, 10, 20)).ToList()
            };
            var active = new DialogFrameSetDto { Frames = [new(1001, 30, 40)] };
            var state = new ProcessingState { ReusesCompletedFrames = true, Dialog = new() { FrameSets = [done, active] } };
            IncrementalProgressStore.SaveChunked(path, state);
            var chunk = Assert.Single(Directory.GetFiles(path + ".chunks"));
            File.SetLastWriteTimeUtc(chunk, DateTime.UtcNow.AddDays(-1));
            var stamp = File.GetLastWriteTimeUtc(chunk);
            active.Frames.Add(new(1002, 31, 41));
            IncrementalProgressStore.SaveChunked(path, state);
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(chunk));
            Assert.True(new FileInfo(path).Length < JsonSerializer.SerializeToUtf8Bytes(state).Length / 10,
                "Repeated manifest writes must exclude completed per-frame payloads");
            var loaded = IncrementalProgressStore.Load(path);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(loaded));
            using var manifest = JsonDocument.Parse(File.ReadAllText(path));
            Assert.StartsWith("chunks1:", manifest.RootElement.GetProperty("Version").GetString());
            Assert.Equal(done.Frames, loaded.Dialog!.FrameSets[0].Frames);
            Assert.Equal(active.Frames, loaded.Dialog.FrameSets[1].Frames);
            Assert.False(loaded.ReusesCompletedFrames);
            // A full capture after manual editing must not use the immutable-checkpoint identity cache.
            done.Frames.Add(new(1003, 50, 60));
            var edited = new ProcessingState { Dialog = state.Dialog };
            IncrementalProgressStore.SaveChunked(path, edited);
            Assert.Equal(done.Frames, IncrementalProgressStore.Load(path).Dialog!.FrameSets[0].Frames);
            IncrementalProgressStore.Delete(path);
            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(path + ".chunks"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void LegacyFilesLoadAndUncommittedOrCorruptChunksCannotReplaceValidState()
    {
        var root = Path.Combine(Path.GetTempPath(), "SekaiTools-chunks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "task.json");
        try
        {
            ProgressStore.SaveToPath(path, new ProcessingState { FrameIndex = 12 });
            Assert.Equal(12, IncrementalProgressStore.Load(path).FrameIndex);
            IncrementalProgressStore.SaveChunked(path, new ProcessingState
            {
                FrameIndex = 20,
                Marker = new() { FrameSets = [new() { Finished = true, Frames = [new(4, 10, 20)] }] }
            });
            // An interrupted future write leaves orphan/temp files, never a partially updated manifest.
            File.WriteAllText(path + ".interrupted.tmp", "broken");
            File.WriteAllText(Path.Combine(path + ".chunks", new string('A', 64) + ".json"), "orphan");
            Assert.Equal(20, IncrementalProgressStore.Load(path).FrameIndex);
            var referenced = Directory.GetFiles(path + ".chunks").Single(f => !f.EndsWith(new string('A', 64) + ".json"));
            File.WriteAllText(referenced, "corrupted");
            Assert.Throws<InvalidDataException>(() => IncrementalProgressStore.Load(path));
            Assert.Empty(SubtitleQueueProgressStore.EnumerateProgressFiles(root));
            var json = File.ReadAllText(path).Replace(Path.GetFileNameWithoutExtension(referenced), "../outside");
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => IncrementalProgressStore.Load(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
