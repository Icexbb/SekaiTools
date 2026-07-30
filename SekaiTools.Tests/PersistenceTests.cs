using System.Reflection;
using System.Text.Json;
using SekaiDataFetch.List;
using SekaiToolsCore.Process;

namespace SekaiTools.Tests;

public class PersistenceTests
{
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
        Assert.Equal(2, diagnostic.TargetIndex);
        Assert.Equal(300, diagnostic.FrameIndex);
        Assert.Equal("有限前瞻命中后续横幅", diagnostic.Reason);
    }
}
