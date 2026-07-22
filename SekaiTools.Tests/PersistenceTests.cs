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
                Directory.Delete(directory, recursive: true);
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
}
