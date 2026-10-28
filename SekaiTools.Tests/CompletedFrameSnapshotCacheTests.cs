using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class CompletedFrameSnapshotCacheTests
{
    [Fact]
    public void CheckpointsShareFinishedSnapshotsButExportsAndActiveFramesStayFresh()
    {
        var cache = new CompletedFrameSnapshotCache<List<int>, int[]>();
        var frames = new List<int> { 1, 2 };
        var first = cache.Capture(frames, false, true, f => f.ToArray());
        frames.Add(3);
        var second = cache.Capture(frames, false, true, f => f.ToArray());
        Assert.Equal(new[] { 1, 2 }, first);
        Assert.Equal(new[] { 1, 2, 3 }, second);
        var finished = cache.Capture(frames, true, true, f => f.ToArray());
        Assert.Same(finished, cache.Capture(frames, true, true, _ => throw new Exception("copied completed frames")));
        frames.Add(4); // Editing after processing must be visible to normal capture/export.
        Assert.Equal(new[] { 1, 2, 3, 4 }, cache.Capture(frames, true, false, f => f.ToArray()));
        cache.Clear(); // A restored state cannot retain pre-restore snapshots.
        Assert.Equal(new[] { 1, 2, 3, 4 }, cache.Capture(frames, true, true, f => f.ToArray()));
    }
}
