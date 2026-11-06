using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SekaiToolsCore.Timing;
using SekaiToolsCore.Process.Config;
using SekaiToolsGUI.View.Subtitle.Components;
using SekaiToolsGUI.ViewModel.Subtitle;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 3) throw new ArgumentException("VideoTimingProbe <video> <menu.png> <new-output> [--ui]");
        var video = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[2]);
        if (Directory.Exists(output)) throw new IOException("Output must be a new directory");
        Directory.CreateDirectory(output);
        if (!args.Contains("--ui"))
        {
            var result = new VideoTimingRecognizer().Recognize(video, args[1]);
            var report = new
            {
                video,
                fps = result.Video.Fps.Fps(),
                mode = "timing-only",
                ocr_used = false,
                dialogs = result.Dialogs.Select(set => new
                {
                    start = set.StartIndex() / result.Video.Fps.Fps(),
                    end = (set.EndIndex() + 1) / result.Video.Fps.Fps(),
                    speaker = set.Data.CharacterOriginal,
                    body = set.Data.BodyOriginal
                })
            };
            File.WriteAllText(Path.Combine(output, "recognition.json"), JsonSerializer.Serialize(report));
            return;
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
        var dialog = new SubtitleTaskDialog(new Wpf.Ui.Controls.ContentDialogHost());
        dialog.ViewModel.IsTimingOnly = true;
        dialog.ViewModel.VideoFilePath = video;
        Require(dialog.IsPrimaryButtonEnabled, "Video-only task dialog submission without script/translation");
        var state = new SubtitlePageModel { IsTimingOnly = true, VideoFilePath = video, ShowPreview = true };
        Require(state.CanStart && state.ScriptFilePath == "" && state.TranslateFilePath == "", "Video-only setup");
        var control = new SubtitleTask(state);
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
        control.Foreground = (Brush)app.FindResource("TextFillColorPrimaryBrush");
        var previewCount = 0;
        DateTime? disabledAt = null;
        var resumed = false;
        state.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName != nameof(SubtitlePageModel.FramePreviewImage) || !state.IsRunning) return;
            previewCount++;
            if (previewCount == 3)
            {
                state.ShowPreview = false;
                disabledAt = DateTime.UtcNow;
            }
        };
        var run = (Task)typeof(SubtitleTask).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [null])!;
        while (!run.IsCompleted)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            if (!resumed && disabledAt.HasValue && DateTime.UtcNow - disabledAt.Value >= TimeSpan.FromMilliseconds(400))
            {
                Require(previewCount == 3, "Disabled preview must stop updates");
                state.ShowPreview = true;
                resumed = true;
            }
            Thread.Sleep(10);
        }
        run.GetAwaiter().GetResult();
        Require(resumed && previewCount > 3, "Running preview must update and resume after toggling");
        Require(state.IsFinished && state.CanEditLine && state.CanOutput, "Finished task must be editable/exportable");
        var process = (SubtitleTaskProcess)control.FindName("ProcessView");
        var panel = (StackPanel)process.FindName("LinePanel");
        var lines = panel.Children.OfType<DialogLine>().ToArray();
        Require(lines.Length == 10, "Expected ten dialogue rows");
        Require(lines.All(line => line.ViewModel.Set.Data.BodyOriginal == ""), "Must not invent source text");
        lines[0].ViewModel.TranslatedContent = "手动填写的翻译";
        var set = lines[0].ViewModel.Set;
        var originalStart = set.StartIndex();
        set.SetFrameRange(originalStart + 1, set.EndIndex() - 1);
        lines[0].RefreshTiming();
        lines[0].BringIntoView();
        var subtitle = typeof(SubtitleTask).GetMethod("GenerateSubtitle", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [new ExportStyleConfig()])!.ToString()!;
        Require(subtitle.Contains("手动填写的翻译") && !subtitle.Contains("未识别原文"), "Export only the filled translation");
        File.WriteAllText(Path.Combine(output, "edited.ass"), subtitle);
        control.Measure(new Size(1200, 850));
        control.Arrange(new Rect(0, 0, 1200, 850));
        control.UpdateLayout();
        ((ScrollViewer)process.FindName("LineViewer")).ScrollToTop();
        control.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1200, 850, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        using (var stream = File.Create(Path.Combine(output, "editor.png")))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
        var setupState = new SubtitlePageModel { IsTimingOnly = true };
        var setup = new SubtitleTaskSetup { DataContext = setupState };
        setup.Measure(new Size(650, 850));
        setup.Arrange(new Rect(0, 0, 650, 850));
        setup.UpdateLayout();
        foreach (var name in new[] { "ScriptFileCard", "TranslationFileCard", "AutoSelectFilesCheckBox" })
            Require(((FrameworkElement)setup.FindName(name)).Visibility == Visibility.Collapsed, "Timing-only selection hidden: " + name);
        setupState.IsTimingOnly = false;
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        setup.UpdateLayout();
        foreach (var name in new[] { "ScriptFileCard", "TranslationFileCard", "AutoSelectFilesCheckBox" })
            Require(((FrameworkElement)setup.FindName(name)).Visibility == Visibility.Visible, "Script-mode selection restored: " + name);
        Require(VisualNodes(setup).OfType<Wpf.Ui.Controls.SymbolIcon>().Any(icon => icon.Symbol == Wpf.Ui.Controls.SymbolRegular.Beaker24), "Experimental icon");
        Console.WriteLine("PASS: material visibility switch, running preview toggle/resume, existing editing, timing edit, translated-only ASS and WPF render");
        var dispose = (Task)typeof(SubtitleTask).GetMethod("DisposeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, null)!;
        while (!dispose.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        dispose.GetAwaiter().GetResult();
        var canceledState = new SubtitlePageModel { IsTimingOnly = true, VideoFilePath = video };
        var canceledControl = new SubtitleTask(canceledState);
        var canceledRun = (Task)typeof(SubtitleTask).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(canceledControl, [null])!;
        typeof(SubtitleTask).GetMethod("RequestStop", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(canceledControl, null);
        while (!canceledRun.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        canceledRun.GetAwaiter().GetResult();
        Require(canceledState.IsCanceled && !canceledState.IsRunning && !canceledState.IsFinished, "Cancellation state");
        var canceledDispose = (Task)typeof(SubtitleTask).GetMethod("DisposeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(canceledControl, null)!;
        while (!canceledDispose.IsCompleted) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        canceledDispose.GetAwaiter().GetResult();
        Console.WriteLine("PASS: cancellation and disposal");
        app.Shutdown();
    }

    private static IEnumerable<DependencyObject> VisualNodes(DependencyObject parent)
    {
        yield return parent;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var node in VisualNodes(VisualTreeHelper.GetChild(parent, i))) yield return node;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
