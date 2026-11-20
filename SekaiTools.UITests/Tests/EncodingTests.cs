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

    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_ENCODING_005_输出路径冲突显示失败摘要并清理临时文件() => RunEncodingCase(false, failOutput: true);

    private static void RunEncodingCase(bool burnSubtitle, bool cancel = false, bool failOutput = false)
    {
        using var server = new LoopbackHttpFixture();
        using var app = new ApplicationFixture(failOutput ? "UI-ENCODING-005" : cancel ? "UI-ENCODING-003" : burnSubtitle ? "UI-ENCODING-002" : "UI-ENCODING-001", "Functional");
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
            var ffmpeg = Path.Combine(pack, "ffmpeg.exe");
            if (cancel)
            {
                var segment = Path.Combine(app.Data.Root, "segment.mp4");
                FfmpegFixture.Run(ffmpeg, Path.Combine(app.Data.Directory, "generate-segment.log"), "-y", "-f", "lavfi",
                    "-i", "color=c=blue:s=1280x720:r=24", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", segment);
                FfmpegFixture.Run(ffmpeg, Path.Combine(app.Data.Directory, "generate-video.log"), "-y", "-stream_loop", "299",
                    "-i", segment, "-c", "copy", "-fs", "16M", video);
                var first = Path.Combine(app.Data.Root, "first.mp4");
                FfmpegFixture.Run(ffmpeg, Path.Combine(app.Data.Directory, "generate-pending.log"), "-y", "-stream_loop", "29",
                    "-i", segment, "-c", "copy", first);
                File.Copy(first, Path.Combine(app.Data.Root, "second.mp4"));
            }
            else
                FfmpegFixture.Run(ffmpeg, Path.Combine(app.Data.Directory, "generate-video.log"),
                    "-y", "-f", "lavfi", "-i", "color=c=blue:s=320x240:r=24", "-t", "2", "-c:v", "libx264", "-pix_fmt", "yuv420p", video);
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
            AddVideo(video, failOutput);
        });

        void AddVideo(string path, bool occupyOutput = false)
        {
            var add = app.Finder.ById(app.Window!, "Encoding.AddTaskButton");
            WaitHelper.Until(() => add.IsEnabled, "压制运行库准备完成", TimeSpan.FromSeconds(60));
            add.AsButton().Invoke();
            var dialog = app.Finder.ById(app.Window!, "Encoding.AddTaskDialog");
            Assert.DoesNotContain(dialog.FindAllDescendants(cf => cf.ByName("添加任务").And(cf.ByControlType(ControlType.Button))), button => button.IsEnabled);
            app.Finder.ById(dialog, "Encoding.VideoBrowseButton").AsButton().Invoke();
            var picker = app.OwnedFileDialog();
            WaitHelper.Until(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("1148").And(cf.ByControlType(ControlType.Edit))), "视频文件名框").AsTextBox().Text = path;
            Keyboard.Type(VirtualKeyShort.RETURN);
            var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("添加任务").And(cf.ByControlType(ControlType.Button))), "添加任务按钮");
            WaitHelper.Until(() => submit.IsEnabled, "小视频分析与提交校验通过", TimeSpan.FromSeconds(30));
            if (occupyOutput)
                Directory.CreateDirectory(Path.Combine(app.Data.Root, "[STVS]input.mp4"));
            submit.AsButton().Invoke();
            WaitHelper.Until(() => app.Window!.FindFirstDescendant(cf => cf.ByAutomationId("Encoding.AddTaskDialog")) is null,
                "添加任务表单已关闭");
        }
        if (failOutput)
        {
            app.Step("失败摘要", "无法发布输出时显示异常摘要并保留冲突目录", () =>
            {
                var window = app.Window!;
                var summary = app.Finder.ById(window, "Encoding.FailureSummary");
                WaitHelper.Until(() => !summary.IsOffscreen && summary.Name.Contains("压制失败", StringComparison.Ordinal),
                    "失败原因已显示", TimeSpan.FromSeconds(30));
                Assert.Contains("[STVS]input.mp4", summary.Name);
                Assert.Contains("压制失败", app.Finder.ById(window, "Encoding.TaskLog").AsTextBox().Text);
                Assert.True(Directory.Exists(Path.Combine(app.Data.Root, "[STVS]input.mp4")));
                Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(video)));
                WaitHelper.Until(() => !Directory.EnumerateFiles(app.Data.Temp, "*.partial.mp4", SearchOption.AllDirectories).Any(),
                    "失败后清理临时输出");
                ScreenshotHelper.CaptureStable(window, Path.Combine(app.Data.Directory, "Screenshots"), "Encoding.OutputFailure");
            });
            app.Step("退出", "压制失败后正常退出", app.Close);
            return;
        }
        if (cancel)
        {
            app.Step("取消真实编码", "临时视频已开始写入后取消，日志正确且临时文件清理", () =>
            {
                var output = Path.Combine(app.Data.Root, "[STVS]input.mp4");
                var partial = WaitHelper.Until(() => Directory.EnumerateFiles(app.Data.Temp, "*.partial.mp4", SearchOption.AllDirectories)
                    .FirstOrDefault(path => new FileInfo(path).Length > 0), "真实FFmpeg开始写入临时视频", TimeSpan.FromSeconds(30));
                var window = app.Window!;
                AddVideo(Path.Combine(app.Data.Root, "first.mp4"));
                AddVideo(Path.Combine(app.Data.Root, "second.mp4"));
                var first = Task("first.mp4");
                var second = Task("second.mp4");
                Assert.NotNull(first.FindFirstDescendant(cf => cf.ByName("等待中")));
                Assert.NotNull(second.FindFirstDescendant(cf => cf.ByName("等待中")));
                Assert.True(first.BoundingRectangle.Top < second.BoundingRectangle.Top);
                var grips = window.FindAllDescendants(cf => cf.ByAutomationId("Encoding.Queue.ReorderGrip"))
                    .OrderBy(grip => grip.BoundingRectangle.Top).ToArray();
                Assert.Equal(2, grips.Length);
                var from = grips[1].BoundingRectangle;
                var to = first.BoundingRectangle;
                Mouse.Drag(new System.Drawing.Point(from.Left + from.Width / 2, from.Top + from.Height / 2),
                    new System.Drawing.Point(to.Left + to.Width / 2, to.Top + to.Height / 4), MouseButton.Left);
                WaitHelper.Until(() => Task("second.mp4").BoundingRectangle.Top < Task("first.mp4").BoundingRectangle.Top,
                    "两个等待任务完成换序");
                Assert.NotNull(Task("first.mp4").FindFirstDescendant(cf => cf.ByName("等待中")));
                Assert.NotNull(Task("second.mp4").FindFirstDescendant(cf => cf.ByName("等待中")));
                Task("input.mp4").Invoke();
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
                WaitHelper.Until(() => Task("second.mp4").FindFirstDescendant(cf => cf.ByName("正在编码")), "新队首先开始压制");
                Assert.NotNull(Task("first.mp4").FindFirstDescendant(cf => cf.ByName("等待中")));
                app.Data.WriteJson("canceled-output", new { Temporary = partial, Removed = true, TargetPublished = false });
                ScreenshotHelper.CaptureStable(app.Window!, Path.Combine(app.Data.Directory, "Screenshots"), "Encoding.Canceled");

                Button Task(string name) => WaitHelper.Until(() => window.FindFirstDescendant(cf =>
                    cf.ByName(name).And(cf.ByControlType(ControlType.Button))), $"任务卡片 {name}").AsButton();
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
        if (!burnSubtitle)
        {
            app.Step("筛选与重试", "完成筛选正确，取消覆盖保留旧文件，授权后重试生成可解码视频", () =>
            {
                var window = app.Window!;
                var filter = app.Finder.ById(window, "Encoding.CompletedFilter").AsComboBox();
                filter.Select(2);
                WaitHelper.Until(() => FindTask() is null, "失败筛选隐藏成功任务");
                filter.Select(1);
                WaitHelper.Until(FindTask, "成功筛选显示已完成任务").AsButton().Invoke();
                var retry = WaitHelper.Until(() => window.FindFirstDescendant(cf => cf.ByName("修改后重试")
                    .And(cf.ByControlType(ControlType.Button))), "已完成任务的重试入口");
                var output = Path.Combine(app.Data.Root, "[STVS]input.mp4");
                byte[] previous = [1, 2, 3, 4];
                File.WriteAllBytes(output, previous);
                retry.AsButton().Invoke();
                var dialog = app.Finder.ById(window, "Encoding.AddTaskDialog");
                var submit = WaitHelper.Until(() => dialog.FindFirstDescendant(cf => cf.ByName("添加任务")
                    .And(cf.ByControlType(ControlType.Button))), "重试提交按钮").AsButton();
                WaitHelper.Until(() => submit.IsEnabled, "重试素材分析完成");
                submit.Invoke();
                var confirmation = app.OwnedWindow("Encoding.OverwriteConfirmation");
                WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("返回修改")
                    .And(cf.ByControlType(ControlType.Button))), "拒绝覆盖").AsButton().Invoke();
                WaitHelper.Until(() => submit.IsEnabled, "返回编辑压制任务");
                Assert.Equal(previous, File.ReadAllBytes(output));
                submit.Invoke();
                confirmation = app.OwnedWindow("Encoding.OverwriteConfirmation");
                WaitHelper.Until(() => confirmation.FindFirstDescendant(cf => cf.ByName("继续覆盖")
                    .And(cf.ByControlType(ControlType.Button))), "授权覆盖").AsButton().Invoke();
                WaitHelper.Until(() => new FileInfo(output).Length > previous.Length, "重试成功后替换旧文件", TimeSpan.FromSeconds(60));
                FfmpegFixture.Run(Path.Combine(pack, "ffmpeg.exe"), Path.Combine(app.Data.Directory, "decode-retry.log"),
                    "-v", "error", "-xerror", "-i", output, "-f", "null", "-");
                Assert.Equal(inputHash, SHA256.HashData(File.ReadAllBytes(video)));

                AutomationElement? FindTask() => window.FindFirstDescendant(cf => cf.ByName("input.mp4")
                    .And(cf.ByControlType(ControlType.Button)));
            });
        }
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
