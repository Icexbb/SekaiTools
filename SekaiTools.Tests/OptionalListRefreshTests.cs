using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SekaiDataFetch.List;

namespace SekaiTools.Tests;

public class OptionalListRefreshTests
{
    private sealed class TestList : BaseListStory
    {
        public static string Root { get; set; } = "";
        public static string Server { get; set; } = "";
        [CachePath("required")] private static string RequiredCache => Path.Combine(Root, "required.json");
        [CachePath("optional")] private static string OptionalCache => Path.Combine(Root, "optional.json");
        [SourcePath("required")] private static string RequiredSource => Server + "required";
        [SourcePath("optional", optional: true)] private static string OptionalSource => Server + "optional";
        public bool Loaded { get; private set; }
        protected override void Load() => Loaded = File.Exists(RequiredCache);
    }

    private sealed class InlineProgress : IProgress<ListRefreshProgress>
    {
        public List<ListRefreshProgress> Reports { get; } = [];
        public void Report(ListRefreshProgress value) => Reports.Add(value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalFailurePreservesRequiredDataAndRemovesStaleCache(bool httpFailure)
    {
        await RunServerTest(httpFailure ? "httpFailure" : "optionalInvalid", async root =>
        {
            await File.WriteAllTextAsync(Path.Combine(root, "optional.json"), "[\"old source\"]");
            var list = new TestList();
            var progress = new InlineProgress();
            await list.Refresh(progress);
            Assert.True(list.Loaded);
            Assert.Equal("[]", await File.ReadAllTextAsync(Path.Combine(root, "required.json")));
            Assert.False(File.Exists(Path.Combine(root, "optional.json")));
            Assert.Equal(ListRefreshStage.Completed, progress.Reports[^1].Stage);
            Assert.Equal(1, progress.Reports[^1].UnavailableFiles);
            Assert.Equal(100, progress.Reports[^1].Percentage);
        });
    }

    [Fact]
    public async Task RequiredFailureDoesNotReplaceCachesOrLoadPartialData()
    {
        await RunServerTest("requiredInvalid", async root =>
        {
            await File.WriteAllTextAsync(Path.Combine(root, "required.json"), "[\"original\"]");
            var list = new TestList();
            await Assert.ThrowsAnyAsync<JsonException>(() => list.Refresh());
            Assert.False(list.Loaded);
            Assert.Equal("[\"original\"]", await File.ReadAllTextAsync(Path.Combine(root, "required.json")));
        });
    }

    [Fact]
    public async Task SuccessfulRefreshSavesAllFilesAndReportsCompletion()
    {
        await RunServerTest("success", async root =>
        {
            var list = new TestList();
            var progress = new InlineProgress();
            await list.Refresh(progress);
            Assert.True(list.Loaded);
            Assert.True(File.Exists(Path.Combine(root, "optional.json")));
            Assert.Equal(2, progress.Reports[^1].CompletedFiles);
            Assert.Equal(0, progress.Reports[^1].UnavailableFiles);
            Assert.Equal(ListRefreshStage.Completed, progress.Reports[^1].Stage);
        });
    }

    private static async Task RunServerTest(string mode, Func<string, Task> test)
    {
        var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var root = Path.Combine(Path.GetTempPath(), "SekaiToolsRefreshTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var listener = new HttpListener();
        TestList.Root = root;
        TestList.Server = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(TestList.Server);
        listener.Start();
        var server = Task.Run(async () =>
        {
            try
            {
                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    var optional = context.Request.Url!.AbsolutePath == "/optional";
                    var invalid = optional ? mode == "optionalInvalid" : mode == "requiredInvalid";
                    context.Response.StatusCode = optional && mode == "httpFailure" ? 404 : 200;
                    var bytes = Encoding.UTF8.GetBytes(invalid ? "invalid json" : "[]");
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException) { }
        });
        try { await test(root); }
        finally
        {
            listener.Stop();
            await server;
            Directory.Delete(root, recursive: true);
        }
    }
}
