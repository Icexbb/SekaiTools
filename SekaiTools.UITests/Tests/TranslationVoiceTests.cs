using System.Text.Json;
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
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_012_原始语音缺失可重试()
    {
        const string request = "GET /assets/sound/scenario/voice/ui_voice/voice_01.mp3 HTTP/1.1";
        using var server = new LoopbackHttpFixture { NotFound = line => line == request };
        using var app = new ApplicationFixture("UI-TRANSLATION-012", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var config = Path.Combine(app.Data.Root, "network-endpoints.json");
        File.WriteAllText(config, File.ReadAllText(config).Replace(":9/assets/", $":{server.Port}/assets/"));
        var path = Path.Combine(app.Data.Root, "ui_voice.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new GameScript
        {
            ScenarioId = "ui_voice",
            TalkData = [new("角色", "有语音。", 0, Voices: [new(0, "voice_01")]), new("角色", "无语音。", 1)],
            Snippets = [new() { Action = 1 }, new() { Action = 1, ReferenceIndex = 1 }]
        }));
        app.Step("载入原始语音样本", "只有带语音的对话显示预览按钮", () =>
        {
            app.Start();
            ui.Load(Catalog.LoadScript, path);
            ui.Body(2);
            Assert.Single(app.Window!.FindAllDescendants(cf => cf.ByName("原始角色语音预览")));
        });
        app.Step("404与重试", "显示语音不存在提示，两次点击均发出请求且不留下缓存", () =>
        {
            var button = Assert.Single(app.Window!.FindAllDescendants(cf => cf.ByName("原始角色语音预览"))).AsButton();
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                button.Invoke();
                WaitHelper.Until(() => server.Requests.Count(line => line == request) == attempt &&
                    button.Properties.HelpText.ValueOrDefault == "语音文件不存在（404），可点击重试", "语音404反馈");
                Assert.True(button.IsEnabled);
            }

            var cache = app.Data.Paths.VoiceCache;
            Assert.False(Directory.Exists(cache) && Directory.EnumerateFiles(cache, "*.mp3", SearchOption.AllDirectories).Any());
            app.Data.WriteJson("voice-requests", server.Requests.ToArray());
            app.Close();
        });
    }
}
