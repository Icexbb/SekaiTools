using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsGUI.ViewModel.Translate;
using Script = SekaiToolsBase.GameScript.GameScript;

namespace SekaiTools.Tests;

public class FlashbackTests
{
    [Theory]
    [InlineData("voice_ev_street_18_06_98b_67", "ev_street_18_06")]
    [InlineData("voice_card_ev_wl_wonder_01_15_4a_20_15", "card_ev_wl_wonder_01_15_4a")]
    [InlineData("voice_ev_night__06_20_19", "ev_night__06")]
    [InlineData("voice_ms_night13_28_18", "ms_night13")]
    [InlineData("partvoice_28_021_band", null)]
    [InlineData("areatalk_ev_band_02_004_006", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void 语音来源解析(string? voice, string? expected) => Assert.Equal(expected, FlashbackAnalyzer.GetSource(voice));

    private static Talk Line(params string[] voices) => new("角色", "台词", 0,
        voices.Select(voice => new Voice(0, voice)).ToArray());

    [Fact]
    public void 来源按行去重且忽略通用未知和无语音()
    {
        var result = FlashbackAnalyzer.Analyze([
            Line("voice_ev_old_01_01_01", "voice_ev_old_01_02_02", "voice_ev_old_01_03_03"),
            Line("voice_ev_current_02_01_01"), Line("voice_ev_current_02_02_01"),
            Line("partvoice_28_021_band"), Line("unknown"), Line(),
            Line("voice_ev_current_02_03_01", "voice_ev_old_01_04_01")]);
        Assert.True(result[0]!.IsFlashback);
        Assert.Equal("ev_current_02", result[0]!.MainSource);
        Assert.All(result[1..6], item => Assert.False(item!.IsFlashback));
        Assert.True(result[6]!.IsFlashback);
        Assert.Contains("ev_old_01", result[6]!.Hint);
        Assert.Null(FlashbackAnalyzer.Analyze([Line("unknown")])[0]);
    }

    [Fact]
    public void 翻译重载清空和切换剧本正确刷新标识且不污染导出()
    {
        var script = new Script
        {
            ScenarioId = "test",
            TalkData = [Line("voice_ev_current_01_01_01"), Line("voice_ev_old_01_01_01"), Line("voice_ev_current_01_02_01")],
            Snippets = [new() { Action = 1 }, new() { Action = 1 }, new() { Action = 1 }]
        };
        var model = new TranslatePageModel();
        model.LoadScript(script, "test.json");
        var originalExport = model.Export();
        model.ApplyFlashbackOrigins(new Dictionary<string, FlashbackEventOrigin>
        {
            ["ev_old_01"] = new(135, 135, "来源活动")
        });
        Assert.Contains("第 135 期 · 来源活动", ((LineDialogModel)model.Events[1]).FlashbackLabel);
        Assert.True(((LineDialogModel)model.Events[1]).IsFlashback);
        model.LoadTranslation(new TranslationData(null), "translation.txt");
        model.ClearTranslation();
        Assert.True(((LineDialogModel)model.Events[1]).IsFlashback);
        Assert.Equal(originalExport, model.Export());
        Assert.Contains("第 135 期", ((LineDialogModel)model.Events[1]).FlashbackLabel);
        Assert.False(model.IsDirty);
        model.LoadScript(new Script { TalkData = [Line()], Snippets = [new() { Action = 1 }] }, "next.json");
        Assert.False(((LineDialogModel)model.Events[0]).IsFlashback);
        model.Clear();
        Assert.Empty(model.Events);
    }
}
