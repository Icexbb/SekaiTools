using System.Text.Json;
using System.Text.Json.Nodes;
using FlaUI.Core.AutomationElements;
using SekaiToolsBase.GameScript;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationVoiceTests
{
    private const string VoiceRequest = "GET /assets/sound/scenario/voice/ui_voice/voice_01.mp3 HTTP/1.1";
    private static readonly string BackupVoiceRequest = VoiceRequest.Replace("/assets/", "/backup-assets/", StringComparison.Ordinal);

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_012_原始语音缺失可重试()
    {
        using var server = new LoopbackHttpFixture { NotFound = line => line == VoiceRequest };
        using var app = new ApplicationFixture("UI-TRANSLATION-012", "Functional");
        app.Step("载入原始语音样本", "只有带语音的对话显示预览按钮", () =>
        {
            LoadVoiceScript(app, server);
            VoiceButton(app);
        });
        app.Step("404与重试", "显示语音不存在提示，两次点击均发出请求且不留下缓存", () =>
        {
            var button = VoiceButton(app);
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                button.Invoke();
                WaitHelper.Until(() => server.Requests.Count(line => line == VoiceRequest) == attempt &&
                    button.Properties.HelpText.ValueOrDefault == "语音文件不存在（404），可点击重试", "语音404反馈");
                Assert.True(button.IsEnabled);
            }

            var cache = app.Data.Paths.VoiceCache;
            Assert.False(Directory.Exists(cache) && Directory.EnumerateFiles(cache, "*.mp3", SearchOption.AllDirectories).Any());
            app.Data.WriteJson("voice-requests", server.Requests.ToArray());
            app.Close();
        });
    }

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_013_播放合成语音并复用缓存()
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture("UI-TRANSLATION-013", "Functional");
        var audioPath = Path.Combine(app.Data.Root, "preview.mp3");
        var bundledFfmpeg = Path.Combine(ResourcePackFixture.DirectoryFor("vapourSynth"), "ffmpeg.exe");
        FfmpegFixture.Run(File.Exists(bundledFfmpeg) ? bundledFfmpeg : "ffmpeg.exe",
            Path.Combine(app.Data.Directory, "generate-voice.log"), "-f", "lavfi", "-i", "sine=frequency=440:duration=3",
            "-af", "volume=0.05", "-ar", "44100", "-ac", "2", "-c:a", "libmp3lame", "-b:a", "128k",
            "-write_xing", "0", audioPath);
        var audio = File.ReadAllBytes(audioPath);
        server.BinaryResponse = line => line == BackupVoiceRequest ? audio : null;
        app.Step("载入剧本与播放", "实际播放器开始并结束，隔离缓存字节与服务端一致", () =>
        {
            LoadVoiceScript(app, server, addBackup: true);
            PlayToEnd(app);
            Assert.Single(server.Requests, line => line == VoiceRequest);
            Assert.Single(server.Requests, line => line == BackupVoiceRequest);
            var cache = Assert.Single(Directory.EnumerateFiles(app.Data.Paths.VoiceCache, "*.mp3", SearchOption.AllDirectories));
            Assert.Equal(audio, File.ReadAllBytes(cache));
        });
        app.Step("再次播放", "再次完整播放且已缓存的备用源音频不重复下载", () =>
        {
            PlayToEnd(app);
            Assert.Single(server.Requests, line => line == BackupVoiceRequest);
            app.Data.WriteJson("voice-requests", server.Requests.ToArray());
            app.Close();
        });
    }

    private static Button VoiceButton(ApplicationFixture app) =>
        Assert.Single(app.Window!.FindAllDescendants(cf => cf.ByName("原始角色语音预览"))).AsButton();

    private static void PlayToEnd(ApplicationFixture app)
    {
        var button = VoiceButton(app);
        button.Invoke();
        WaitHelper.Until(() => !button.IsEnabled && button.Properties.HelpText.ValueOrDefault == "正在播放原始语音", "真实播放器开始");
        WaitHelper.Until(() => button.IsEnabled && button.Properties.HelpText.ValueOrDefault == "播放原始语音", "真实播放器结束");
    }

    private static void LoadVoiceScript(ApplicationFixture app, LoopbackHttpFixture server, bool addBackup = false)
    {
        var config = Path.Combine(app.Data.Root, "network-endpoints.json");
        var configuration = File.ReadAllText(config).Replace(":9/", $":{server.Port}/");
        File.WriteAllText(config, configuration);
        var sources = JsonNode.Parse(configuration)!["defaultDataSources"]!.DeepClone().AsArray();
        if (addBackup)
        {
            var backup = sources[0]!.DeepClone();
            backup["SourceName"] = "Backup voice fixture";
            backup["StorageBaseUrl"] = $"http://127.0.0.1:{server.Port}/backup-assets/";
            sources.Add(backup);
        }
        var sourceResponse = sources.ToJsonString();
        server.Response = request => request.Contains("/sources.json ") ? sourceResponse : null;
        var path = Path.Combine(app.Data.Root, "ui_voice.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui_voice",
            TalkData = [new("角色", "有语音。", 0, Voices: [new(0, "voice_01")]), new("角色", "无语音。", 1)],
            Snippets = [new() { Action = 1 }, new() { Action = 1, ReferenceIndex = 1 }]
        }));
        var ui = new TranslationWorkspaceDriver(app);
        app.Start();
        ui.Load(Catalog.LoadScript, path);
        ui.Body(2);
    }
}
