using System.Drawing;
using System.Security.Cryptography;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using SekaiToolsBase.GameScript;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Fixtures;
using SekaiTools.UITests.Helpers;
using Xunit;

namespace SekaiTools.UITests.Tests;

[Collection("Desktop")]
public sealed class TranslationDropTests
{
    [DesktopFact]
    [Trait("Category", "Functional")]
    public void UI_TRANSLATION_014_拖入剧本参考与当前译文()
    {
        using var app = new ApplicationFixture("UI-TRANSLATION-014", "Functional");
        var ui = new TranslationWorkspaceDriver(app);
        var script = Path.Combine(app.Data.Root, "drop.json");
        var reference = Path.Combine(app.Data.Root, "reference.txt");
        var translation = Path.Combine(app.Data.Root, "translation.txt");
        var invalid = Path.Combine(app.Data.Root, "invalid.txt");
        File.WriteAllText(script, JsonSerializer.Serialize(ui.Script));
        File.WriteAllText(reference, "角色：参考甲。\n角色：参考乙。");
        File.WriteAllText(translation, "角色：翻译甲。\n角色：翻译乙。");
        File.WriteAllText(invalid, "角色：条数不匹配。");
        string[] inputs = [script, reference, translation, invalid];
        var hashes = inputs.Select(Hash).ToArray();

        app.Step("拖入剧本与参考", "上半区载入参考且保留当前正文", () =>
        {
            app.Start();
            Drop(script, 0.25);
            ui.Body(2);
            Drop(reference, 0.25);
            Assert.Equal("参考甲。", Reference());
            Assert.Empty(ui.Body(1).Text);
        });
        app.Step("无效拖入与替换", "拒绝不匹配文件，下半区确认后仅替换当前译文", () =>
        {
            Drop(invalid, 0.75, accepted: false);
            Assert.Empty(ui.Body(1).Text);
            Assert.Equal("参考甲。", Reference());
            Drop(translation, 0.75);
            ui.Confirm("继续", "替换当前内容？");
            WaitHelper.Until(() => ui.Body(1).Text == "翻译甲。", "下半区载入当前译文");
            Assert.Equal("翻译乙。", ui.Body(2).Text);
            Assert.Equal("参考甲。", Reference());
            Assert.Equal(hashes, inputs.Select(Hash));
            app.Close();
        });

        string Reference() => app.Finder.ById(app.Window!, "Translation.Dialog.1.Reference").AsTextBox().Text;
        void Drop(string path, double verticalFraction, bool accepted = true)
        {
            var bounds = app.Finder.ById(app.Window!, Catalog.Translation).BoundingRectangle;
            var point = new Point(bounds.Left + bounds.Width / 2, bounds.Top + (int)(bounds.Height * verticalFraction));
            Assert.Equal(accepted, DragFile(path, point));
        }
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static bool DragFile(string path, Point target)
    {
        var ready = new TaskCompletionSource<Point>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dragging = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var source = new System.Windows.Controls.Border { Background = System.Windows.Media.Brushes.LightGray };
                var window = new System.Windows.Window
                {
                    Title = "FlaUI 文件拖放源",
                    Width = 160,
                    Height = 90,
                    Topmost = true,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Content = source
                };
                source.MouseLeftButtonDown += (_, _) =>
                {
                    var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { path });
                    dragging.TrySetResult();
                    var effect = System.Windows.DragDrop.DoDragDrop(source, data, System.Windows.DragDropEffects.Copy);
                    finished.TrySetResult(effect == System.Windows.DragDropEffects.Copy);
                    window.Close();
                };
                window.ContentRendered += (_, _) =>
                {
                    var origin = source.PointToScreen(new System.Windows.Point(source.ActualWidth / 2, source.ActualHeight / 2));
                    ready.TrySetResult(new Point((int)origin.X, (int)origin.Y));
                };
                window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
                window.Show();
                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
                finished.TrySetException(exception);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Mouse.Position = ready.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Mouse.Down(MouseButton.Left);
            try
            {
                dragging.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                Mouse.MoveTo(target);
            }
            finally
            {
                Mouse.Up(MouseButton.Left);
            }
            return finished.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally
        {
            System.Windows.Threading.Dispatcher.FromThread(thread)?.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
            thread.Join(TimeSpan.FromSeconds(5));
        }
    }
}
