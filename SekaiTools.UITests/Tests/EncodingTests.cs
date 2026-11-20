using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class EncodingTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_001_真实资源准备和小视频转码完成() => RunEncodingCase(false);

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_002_ASS实际烧录改变输出画面() => RunEncodingCase(true);

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_003_真实编码运行后取消并清理临时文件() => RunEncodingCase(false, true);

    private static void RunEncodingCase(bool burnSubtitle, bool cancel = false)
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture(cancel ? "UI-ENCODING-003" : burnSubtitle ? "UI-ENCODING-002" : "UI-ENCODING-001", "Functional");
        var executable = Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_EXE")!;
        var pack = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(executable)!, "..", "fixtures", "vapourSynth"));
        var files = new Dictionary<string, byte[]>();
        var manifest = new List<object>();
        var video = Path.Combine(app.Data.Root, "input.mp4");
        app.Step("资源及素材", cancel ? "固定运行库和十分钟合成视频就绪" : "固定版本真实运行库和两秒合成视频就绪", () =>
        {
            using var resourceLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "media-resources.lock.json")));
            foreach (var entry in resourceLock.RootElement.EnumerateArray())
            {
                var name = entry.GetProperty("Name").GetString()!;
                var path = Path.GetFullPath(Path.Combine(pack, name));
                Assert.True(SekaiToolsConfiguration.ApplicationPaths.ContainsPath(pack, path));
                Assert.True(File.Exists(path), "请先运行 Tools/PrepareUIMediaResources.ps1 -SourceDirectory <运行库目录>。");
                var bytes = File.ReadAllBytes(path);
                Assert.Equal(entry.GetProperty("Sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
                files.Add(name, bytes);
                manifest.Add(new { Path = "vapourSynth/" + name, Size = bytes.Length, Md5 = Convert.ToHexString(MD5.HashData(bytes)) });
            }
            var config = Path.Combine(app.Data.Root, "network-endpoints.json");
            File.WriteAllText(config, File.ReadAllText(config).Replace(":9/", $":{server.Port}/"));
            server.Response = request => request.Contains("/resources/vapourSynth.json ") ? JsonSerializer.Serialize(manifest) : null;
            server.BinaryResponse = request => files.FirstOrDefault(file => request.Contains("/resources/vapourSynth/" + file.Key + " ")).Value;
            FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), Path.Combine(app.Data.Directory, "generate-video.log"),
                "-y", "-f", "lavfi", "-i", "color=c=blue:s=320x240:r=24", "-t", cancel ? "600" : "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", video);
            if (burnSubtitle)
                File.WriteAllText(Path.ChangeExtension(video, ".ass"), """
                    [Script Info]
                    ScriptType: v4.00+
                    PlayResX: 320
                    PlayResY: 240
                    [V4+ Styles]
                    Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
                    Style: Default,Arial,20,&H0000FF00,&H0000FF00,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1
                    [Events]
                    Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
                    Dialogue: 0,0:00:00.00,0:00:02.00,Default,,0,0,0,,{\an7\pos(100,60)\p1\bord0\shad0\1c&H00FF00&}m 0 0 l 120 0 120 120 0 120
                    """);
        });
        var inputHash = SHA256.HashData(File.ReadAllBytes(video));
        var subtitleHash = burnSubtitle ? SHA256.HashData(File.ReadAllBytes(Path.ChangeExtension(video, ".ass"))) : null;
        app.Step("资源准备与创建任务", "真实页面下载校验运行库并完成素材分析", () =>
        {
            app.Start();
            app.Navigate("MainWindow.Navigation.Encoding", "Encoding.Root");
            var add = app.Finder.ById(app.Window!, "Encoding.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "压制运行库准备完成", TimeSpan.FromSeconds(60));
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Encoding.AddTaskDialog");
            Assert.DoesNotContain(dialog.FindAllDescendants(cf => cf.ByName("添加任务").And(cf.ByControlType(ControlType.Button))), button => button.IsEnabled);
            app.Finder.ById(dialog, "Encoding.VideoBrowseButton").AsButton().Invoke();
            var picker = app.OwnedFileDialog();
            WaitHelper.Until(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "视频文件名框").AsTextBox().Text = video;
            Keyboard.Type(VirtualKeyShort.RETURN);
            var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("添加任务").And(cf.ByControlType(ControlType.Button))), "添加任务按钮");
            WaitHelper.Until(() => submit.IsEnabled, "小视频分析与提交校验通过", TimeSpan.FromSeconds(30));
            submit.AsButton().Invoke();
        });
        if (cancel)
        {
            app.Step("取消真实编码", "临时视频已开始写入后取消，日志正确且临时文件清理", () =>
            {
                var output = Path.Combine(app.Data.Root, "[STVS]input.mp4");
                var partial = WaitHelper.Until(() => Directory.EnumerateFiles(app.Data.Temp, "*.partial.mp4", SearchOption.AllDirectories)
                    .FirstOrDefault(path => new FileInfo(path).Length > 0), "真实FFmpeg开始写入临时视频", TimeSpan.FromSeconds(30));
                var stop = app.Finder.ById(app.Window!, "Encoding.CancelButton");
                WaitHelper.Until(() => stop.IsEnabled, "取消按钮可用");
                stop.AsButton().Invoke();
                WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已取消")), "编码取消完成", TimeSpan.FromSeconds(30));
                Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(video)));
                var log = app.Finder.ById(app.Window!, "Encoding.TaskLog").AsTextBox().Text;
                Assert.Contains("压制已取消", log);
                File.WriteAllText(Path.Combine(app.Data.Directory, "canceled-task.log"), log);
                WaitHelper.Until(() => !File.Exists(partial), "临时视频清理完成");
                Assert.False(File.Exists(output));
                app.Data.WriteJson("canceled-output", new { Temporary = partial, Removed = true, TargetPublished = false });
                ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Encoding.Canceled");
            });
            app.Step("退出", "取消后正常退出", app.Close);
            return;
        }
        app.Step(burnSubtitle ? "真实ASS烧录" : "真实转码",
            burnSubtitle ? "输出48帧且中心由蓝色变为ASS绿色绘图，视频与字幕源文件不变" : "队列任务完成且输出能被FFmpeg完整解码，源文件不变", () =>
        {
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByName("已完成")), "真实转码完成", TimeSpan.FromSeconds(60));
            var window = app.Window!;
            var advanced = app.Finder.ById(window, "Encoding.AdvancedDetails").Patterns.ExpandCollapse.Pattern;
            Assert.Equal(ExpandCollapseState.Collapsed, advanced.ExpandCollapseState.Value);
            var log = app.Finder.ById(window, "Encoding.TaskLog").AsTextBox();
            Assert.False(log.IsOffscreen);
            var logText = log.Text;
            var follow = app.Finder.ById(window, "Encoding.FollowLog").AsCheckBox();
            follow.IsChecked = false;
            advanced.Expand();
            var command = app.Finder.ById(window, "Encoding.FfmpegCommand").AsTextBox();
            WaitHelper.Until(() => !command.IsOffscreen && !string.IsNullOrWhiteSpace(command.Text), "高级详情显示实际参数");
            advanced.Collapse();
            Assert.False(follow.IsChecked);
            Assert.Equal(logText, log.Text);
            follow.IsChecked = true;
            var output = Path.Combine(app.Data.Root, "[STVS]input.mp4");
            Assert.True(new FileInfo(output).Length > 0);
            Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(video)));
            var decodeLog = Path.Combine(app.Data.Directory, "decode-output.log");
            FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), decodeLog, "-v", "error", "-i", output, "-map", "0:v:0", "-progress", "pipe:1", "-f", "null", "-");
            Assert.Contains("frame=48", File.ReadAllLines(decodeLog));
            if (burnSubtitle)
            {
                Assert.Equal(subtitleHash, SHA256.HashData(File.ReadAllBytes(Path.ChangeExtension(video, ".ass"))));
                var inputPixel = ReadCenterPixel(video, "input");
                var outputPixel = ReadCenterPixel(output, "output");
                Assert.True(inputPixel[2] > 150 && inputPixel[1] < 70, "合成源画面中心应为蓝色。");
                Assert.True(outputPixel[1] > 150 && outputPixel[0] < 70 && outputPixel[2] < 70, "ASS绘图必须实际烧录为绿色中心区域。");
                FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), Path.Combine(app.Data.Directory, "extract-frame.log"),
                    "-y", "-ss", "1", "-i", output, "-frames:v", "1", Path.Combine(app.Data.Directory, "burned-frame.png"));
                app.Data.WriteJson("burn-pixels", new { Input = inputPixel.Select(value => (int)value), Output = outputPixel.Select(value => (int)value) });
            }
            ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), burnSubtitle ? "Encoding.Burned" : "Encoding.Completed");
            app.Data.WriteJson("fixture-requests", server.Requests.ToArray());
        });
        app.Step("退出", "转码后正常退出", app.Close);

        byte[] ReadCenterPixel(string source, string name)
        {
            var path = Path.Combine(app.Data.Directory, name + "-center.rgb");
            FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), Path.Combine(app.Data.Directory, name + "-pixel.log"),
                "-y", "-ss", "1", "-i", source, "-vf", "crop=32:32:144:104,scale=1:1,format=rgb24", "-frames:v", "1", "-f", "rawvideo", path);
            var pixel = File.ReadAllBytes(path);
            Assert.Equal(3, pixel.Length);
            return pixel;
        }
    }
}
