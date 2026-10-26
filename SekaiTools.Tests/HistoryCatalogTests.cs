using System.Text.Json;
using SekaiToolsCore;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public sealed class HistoryCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"HistoryCatalog-{Guid.NewGuid():N}");

    [Fact]
    public void SummarySkipsMatcherPayloadAndSelectedEntryLoadsFullState()
    {
        var state = new ProcessingState
        {
            StopReason = ProcessStopReason.Completed,
            VideoFilePath = "video.mp4",
            Timecodes = [0, 40, 80],
            Dialog = new DialogMatcherStateDto
            {
                FrameSets = [new DialogFrameSetDto { Frames = [new FrameResultDto(1, 2, 3)] }]
            }
        };
        Write("History/20261019_key.json", JsonSerializer.Serialize(new HistoryEntry { Timestamp = "today", State = state }));
        var entry = Assert.Single(HistoryCatalog.Read(_root));
        Assert.Equal("video.mp4", entry.Summary.VideoFilePath);
        Assert.Equal("today", entry.Timestamp);
        var restored = entry.LoadState();
        Assert.Equal(state.Timecodes, restored.Timecodes);
        Assert.Equal(new FrameResultDto(1, 2, 3), Assert.Single(Assert.Single(restored.Dialog!.FrameSets).Frames));
        // A type-invalid frame payload is not materialized merely to list metadata.
        Write("History/20261019_key.json", File.ReadAllText(entry.FilePath).Replace("\"Timecodes\":[0,40,80]", "\"Timecodes\":\"not an array\""));
        Assert.Single(HistoryCatalog.Read(_root));
        Assert.Throws<JsonException>(() => entry.LoadState());
    }

    [Fact]
    public void CatalogFiltersCompletedProgressAndDeduplicatesLegacyWithoutDeletingFiles()
    {
        var canceled = JsonSerializer.Serialize(new ProcessingState { StopReason = ProcessStopReason.Canceled });
        Write("SubtitleQueueProgress/shared.json", canceled);
        Write("Progress/shared.json", canceled);
        Write("SubtitleQueueProgress/done.json", JsonSerializer.Serialize(new ProcessingState { StopReason = ProcessStopReason.Completed }));
        Write("History/broken.json", "{");
        Write("History/canceled.json", JsonSerializer.Serialize(new HistoryEntry { State = new ProcessingState { StopReason = ProcessStopReason.Canceled } }));
        var entry = Assert.Single(HistoryCatalog.Read(_root));
        Assert.False(entry.IsHistory);
        Assert.Contains("SubtitleQueueProgress", entry.FilePath);
        Assert.Equal(ProcessStopReason.Canceled, entry.LoadState().StopReason);
        Assert.True(File.Exists(Path.Combine(_root, "SubtitleQueueProgress/done.json")));
    }

    private void Write(string relativePath, string json)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
