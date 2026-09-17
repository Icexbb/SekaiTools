using System.Reflection;
using System.Text.Json;
using SekaiDataFetch.List;
using SekaiToolsCore;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class PersistenceTests
{
    [Fact]
    public void 历史记录只接受已完成任务()
    {
        Assert.True(HistoryStore.IsCompleted(new ProcessingState
        {
            StopReason = ProcessStopReason.Completed
        }));
        Assert.False(HistoryStore.IsCompleted(new ProcessingState
        {
            StopReason = ProcessStopReason.Canceled
        }));
        Assert.False(HistoryStore.IsCompleted(new ProcessingState
        {
            StopReason = ProcessStopReason.None
        }));
    }

    [Fact]
    public void 未完成进度只保留最后一条()
    {
        var first = new ProcessingState { StopReason = ProcessStopReason.Canceled, FrameIndex = 10 };
        var latest = new ProcessingState { StopReason = ProcessStopReason.EndOfStream, FrameIndex = 20 };
        var completed = new ProcessingState { StopReason = ProcessStopReason.Completed, FrameIndex = 30 };

        var result = ProgressStore.SelectLatestIncomplete([
            ("first", first, new DateTime(2026, 1, 1)),
            ("latest", latest, new DateTime(2026, 1, 2)),
            ("completed", completed, new DateTime(2026, 1, 3))
        ]);

        var item = Assert.Single(result);
        Assert.Equal("latest", item.SaveKey);
        Assert.Equal(20, item.State.FrameIndex);
    }

    [Fact]
    public void 进度保存会完整替换目标文件并清理临时文件()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SekaiToolsTests-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "progress.json");
        try
        {
            ProgressStore.SaveToPath(path, new ProcessingState { FrameIndex = 10 });
            ProgressStore.SaveToPath(path, new ProcessingState { FrameIndex = 20 });

            var state = JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(path));
            Assert.Equal(20, state?.FrameIndex);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void 卡片剧情能够发现所有缓存路径()
    {
        var property = typeof(BaseListStory).GetProperty(
            "CachePaths", BindingFlags.Instance | BindingFlags.NonPublic);

        var paths = Assert.IsType<string[]>(property?.GetValue(ListCardStory.Instance));

        Assert.Equal(2, paths.Length);
        Assert.All(paths, path => Assert.EndsWith(".json", path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 匹配诊断能够随进度状态序列化往返()
    {
        var state = new ProcessingState
        {
            Timecodes = [0, 40, 90],
            Banner = new BannerMatcherStateDto
            {
                Diagnostics =
                [
                    new MatcherDiagnosticDto
                    {
                        Matcher = "BannerBaseFrameSet",
                        TargetIndex = 2,
                        FrameIndex = 300,
                        Reason = "有限前瞻命中后续横幅"
                    }
                ]
            }
        };

        var restored = JsonSerializer.Deserialize<ProcessingState>(JsonSerializer.Serialize(state));

        var diagnostic = Assert.Single(restored!.Banner!.Diagnostics);
        Assert.Equal([0, 40, 90], restored.Timecodes);
        Assert.Equal(2, diagnostic.TargetIndex);
        Assert.Equal(300, diagnostic.FrameIndex);
        Assert.Equal("有限前瞻命中后续横幅", diagnostic.Reason);
    }
}
