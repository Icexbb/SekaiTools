using System.Diagnostics;
using System.Reflection;
using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionDiagnosticsTests
{
    private sealed class Resources : IMediaResourceProvider
    { public string GetVapourSynthResourcePath(string fileName) => throw new NotSupportedException(); }

    [Fact]
    public void ProgressReplacesSingleLineWhileOtherLogsAreRetained()
    {
        using var suppressor = new VideoSuppressor(new Resources());
        var type = suppressor.GetType();
        var update = type.GetMethod("UpdateLog", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var append = type.GetMethod("AppendLog", BindingFlags.Instance | BindingFlags.NonPublic)!;
        update.Invoke(suppressor, ["ordinary output"]);
        update.Invoke(suppressor, ["frame= 10 fps= 20.0 size=10KiB"]);
        append.Invoke(suppressor, ["[VSPipe] subtitle warning"]);
        update.Invoke(suppressor, ["frame= 20 fps= 25.0 size=20KiB"]);
        update.Invoke(suppressor, ["encoder warning"]);
        VideoSuppressionProgress? result = null;
        suppressor.ProgressChanged += progress => result = progress;
        type.GetMethod("PublishProgress", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(suppressor, null);
        Assert.NotNull(result);
        Assert.Contains("ordinary output", result.Log);
        Assert.Contains("subtitle warning", result.Log);
        Assert.Contains("encoder warning", result.Log);
        Assert.DoesNotContain("frame= 10", result.Log);
        Assert.Contains("frame= 20", result.Log);
        Assert.Equal(20, result.ProcessedFrames);
    }

    [Fact]
    public void CommandDisplayQuotesSpacesQuotesAndTrailingSlashes()
    {
        var start = new ProcessStartInfo("ffmpeg.exe");
        start.ArgumentList.Add(@"C:\video clips\source.mp4");
        start.ArgumentList.Add("a\"b");
        start.ArgumentList.Add("C:\\folder\\");
        Assert.Equal("\"ffmpeg.exe\" \"C:\\video clips\\source.mp4\" \"a\\\"b\" \"C:\\folder\\\\\"", VideoSuppressor.FormatCommand(start));
    }
}
