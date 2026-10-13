using Microsoft.Extensions.Logging;
using SekaiToolsBase;

namespace SekaiTools.Tests;

public class LoggerTests
{
    [Fact]
    public void LogPreservesLiteralBraces()
    {
        var category = Guid.NewGuid().ToString();
        const string message = "进度数据: {\"frame\":123}, {missing}";
        Logger.Log(message, callerMemberName: category);
        var entry = Assert.Single(InMemoryLogSink.Snapshot(), e => e.Category == category);
        Assert.Equal(message, entry.Message);
    }

    [Fact]
    public void StructuredLoggerIncludesExceptionInSharedSink()
    {
        var category = Guid.NewGuid().ToString();
        Logger.CreateLogger(category).LogError(new InvalidOperationException("测试异常"),
            "任务 {Id} 失败", 42);
        var entry = Assert.Single(InMemoryLogSink.Snapshot(), e => e.Category == category);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("任务 42 失败", entry.Message);
        Assert.Contains("测试异常", entry.Message);
    }

    [Fact]
    public void ConcurrentCategoryAccessUsesSameLogger()
    {
        var category = Guid.NewGuid().ToString();
        var instances = new System.Collections.Concurrent.ConcurrentBag<ILogger>();
        Parallel.For(0, 100, _ => instances.Add(Logger.CreateLogger(category)));
        var expected = Logger.CreateLogger(category);
        Assert.All(instances, logger => Assert.Same(expected, logger));
    }
}
