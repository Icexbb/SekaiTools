using System.IO;
using Expression = System.Linq.Expressions.Expression;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("DownloadUxProbe <GUI dll> <new output directory>");
        var guiPath = Path.GetFullPath(args[0]);
        var directory = Path.GetDirectoryName(guiPath)!;
        var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output)) throw new IOException("Use a new report directory");
        Directory.CreateDirectory(output);
        AppDomain.CurrentDomain.AssemblyResolve += (_, eventArgs) =>
        {
            var name = new AssemblyName(eventArgs.Name).Name + ".dll";
            foreach (var root in new[] { directory, Path.Combine(directory, "libs") })
            {
                var path = Path.Combine(root, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        var gui = Assembly.LoadFrom(guiPath);
        var wpf = Assembly.LoadFrom(Path.Combine(directory, "Wpf.Ui.dll"));
        var data = Assembly.LoadFrom(Path.Combine(directory, "SekaiDataFetch.dll"));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var rows = new List<object>();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            app.Resources.MergedDictionaries.Clear();
            var themes = (ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ThemesDictionary", true)!)!;
            var property = themes.GetType().GetProperty("Theme")!;
            property.SetValue(themes, Enum.Parse(property.PropertyType, theme));
            app.Resources.MergedDictionaries.Add(themes);
            app.Resources.MergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ControlsDictionary", true)!)!);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
            foreach (var dimensions in new[] { (1280, 900), (800, 900), (800, 540) })
            {
                var (width, height) = dimensions;
                var page = (FrameworkElement)gui.GetType("SekaiToolsGUI.View.Download.DownloadPage", true)!
                    .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(bool)], null)!.Invoke([false]);
                var host = new Window
                {
                    Content = page,
                    Width = width,
                    Height = height,
                    Left = -15000,
                    Top = -15000,
                    ShowActivated = false,
                    ShowInTaskbar = false,
                    Background = (Brush)app.Resources["ApplicationBackgroundBrush"]
                };
                var list = (ItemsControl)page.FindName("DownloadItemBox");
                ((ContentControl)page.FindName("ContentCard")).Content = new TextBlock
                { Text = "剧情列表区域（离线队列布局验证）", Margin = new Thickness(16) };
                host.SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
                host.Show();
                var manager = wpf.GetType("Wpf.Ui.Appearance.ApplicationThemeManager", true)!;
                var apply = manager.GetMethods().First(method => method.Name == "Apply" && method.GetParameters().Length > 0 &&
                    method.GetParameters()[0].ParameterType.Name == "ApplicationTheme" && method.GetParameters().Skip(1).All(parameter => parameter.IsOptional));
                var values = apply.GetParameters().Select((parameter, index) => index == 0
                    ? Enum.Parse(parameter.ParameterType, theme) : parameter.DefaultValue).ToArray();
                apply.Invoke(null, values);
                Pump();
                var background = (SolidColorBrush)app.Resources["ApplicationBackgroundBrush"];
                if ((background.Color.R > 128) != (theme == "Light")) throw new InvalidDataException("Rendered theme differs from requested theme");
                Save(page, Path.Combine(output, $"{theme}-{width}x{height}-empty.png"));
                foreach (var state in new[] { "Waiting", "Downloading", "Succeeded", "Failed", "Stopped", "Skipped" })
                {
                    var item = (FrameworkElement)Activator.CreateInstance(gui.GetType("SekaiToolsGUI.View.Download.Components.DownloadTask", true)!,
                        ["活动剧情 · 较长标题用于检查换行和操作按钮是否被遮挡 · 第 12 章", "https://download.invalid/story.json", "测试数据源", output])!;
                    item.GetType().GetMethod("ChangeStatus")!.Invoke(item,
                        [Enum.Parse(data.GetType("SekaiDataFetch.DownloadStatus", true)!, state), state == "Failed" ? "连接失败，请检查网络或代理后重试。" : null]);
                    list.Items.Add(item);
                }
                page.GetType().GetMethod("UpdateTaskListState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
                // Settings are lazily loaded by the real page and may apply the saved theme.
                apply.Invoke(null, values);
                Pump();
                if ((((SolidColorBrush)app.Resources["ApplicationBackgroundBrush"]).Color.R > 128) != (theme == "Light"))
                    throw new InvalidDataException("Saved settings changed the requested fixture theme");
                if (Math.Abs(host.ActualHeight - height) > 1) throw new InvalidDataException("Requested fixture height was not applied");
                var queue = (FrameworkElement)page.FindName("QueueCard");
                var start = (FrameworkElement)page.FindName("DownloadButton");
                var startBounds = new Rect(start.TranslatePoint(new Point(), page), start.RenderSize);
                if (startBounds.Bottom > page.ActualHeight + 1 || startBounds.Right > page.ActualWidth + 1 || startBounds.Top < 0)
                    throw new InvalidDataException("Primary action is clipped");
                var compact = width == 800;
                if (Grid.GetRow(queue) != (compact ? 2 : 0)) throw new InvalidDataException("Queue did not reflow");
                if (app.TryFindResource("SystemFillColorCriticalBrush") == null || app.TryFindResource("AccentTextFillColorPrimaryBrush") == null)
                    throw new InvalidDataException("Missing semantic theme resource");
                Save(page, Path.Combine(output, $"{theme}-{width}x{height}-states.png"));
                rows.Add(new
                {
                    Theme = theme,
                    Width = width,
                    Height = height,
                    ActualPageHeight = page.ActualHeight,
                    QueueRow = Grid.GetRow(queue),
                    PrimaryBounds = startBounds.ToString(),
                    States = list.Items.Count,
                    PrimaryVisible = start.IsVisible
                });
                host.Close();
            }
        }
        VerifyInteractions(gui, data, app, output);
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS light/dark, wide/compact, six queue states and primary action visibility");
    }

    private static readonly ConcurrentDictionary<string, int> Requests = new();
    private static bool _release;
    private static async Task FakeDownload(object job, CancellationToken token)
    {
        var title = (string)job.GetType().GetProperty("Title")!.GetValue(job)!;
        Requests.AddOrUpdate(title, 1, (_, count) => count + 1);
        if (title == "story-0" || _release) return;
        await Task.Delay(Timeout.Infinite, token);
    }

    private static void VerifyInteractions(Assembly gui, Assembly data, Application app, string output)
    {
        var type = gui.GetType("SekaiToolsGUI.View.Download.DownloadPage", true)!;
        var page = (FrameworkElement)type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(bool)], null)!.Invoke([false]);
        var host = new Window { Content = page, Width = 1280, Height = 800, Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
        app.MainWindow = host;
        var jobType = data.GetType("SekaiDataFetch.DownloadJob", true)!;
        var policyType = data.GetType("SekaiDataFetch.ExistingFilePolicy", true)!;
        var parameters = new[] { Expression.Parameter(jobType), Expression.Parameter(policyType), Expression.Parameter(typeof(CancellationToken)) };
        var body = Expression.Call(typeof(Program).GetMethod(nameof(FakeDownload), BindingFlags.Static | BindingFlags.NonPublic)!,
            Expression.Convert(parameters[0], typeof(object)), parameters[2]);
        var callback = Expression.Lambda(body, parameters).Compile();
        var batchType = data.GetType("SekaiDataFetch.DownloadBatch", true)!;
        var batch = batchType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke([callback]);
        type.GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, batch);
        var add = type.GetMethod("AddTask")!;
        var prefix = "https://download.invalid/probe-" + Guid.NewGuid().ToString("N") + "-";
        for (var index = 0; index < 8; index++)
            if (!(bool)add.Invoke(page, [$"story-{index}", prefix + index + ".json"])!) throw new InvalidDataException("Task was not added");
        host.Show();
        var start = (System.Windows.Controls.Primitives.ButtonBase)page.FindName("DownloadButton");
        start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        PumpUntil(() => Requests.Count >= 4);
        var list = (ItemsControl)page.FindName("DownloadItemBox");
        var first = list.Items[0];
        var source = first.GetType().GetProperty("SourceName")!.GetValue(first);
        var savePath = first.GetType().GetProperty("SavePath")!.GetValue(first);
        add.Invoke(page, ["next-batch", prefix + "next.json"]);
        if (!((TextBlock)page.FindName("BatchStatusText")).Text.Contains("下一批 1")) throw new InvalidDataException("Next batch count missing");
        if (!((FrameworkElement)page.FindName("ContentCard")).IsEnabled) throw new InvalidDataException("Browsing was disabled");
        if (((FrameworkElement)page.FindName("ChooseDirectoryButton")).IsEnabled) throw new InvalidDataException("Running output directory can change");
        var next = (FrameworkElement)list.Items[8];
        if (!((FrameworkElement)next.FindName("RemoveButton")).IsEnabled) throw new InvalidDataException("Next batch cannot be edited");
        var vm = type.GetProperty("ViewModel")!.GetValue(page)!;
        vm.GetType().GetProperty("CurrentSourceIndex")!.SetValue(vm, 1);
        Pump();
        if (!Equals(source, first.GetType().GetProperty("SourceName")!.GetValue(first)) ||
            !Equals(savePath, first.GetType().GetProperty("SavePath")!.GetValue(first)))
            throw new InvalidDataException("Source switching changed an existing task");
        start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        PumpUntil(() => type.GetField("_downloadCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) == null);
        var stopped = list.Items.Cast<object>().Count(item => item.GetType().GetProperty("Status")!.GetValue(item)!.ToString() == "Stopped");
        if (stopped != 7 || Requests.ContainsKey("next-batch")) throw new InvalidDataException("Stop did not preserve the fixed batch");
        if (((ProgressBar)page.FindName("BatchProgress")).Value != 1) throw new InvalidDataException("Canceled tasks inflate completed progress");
        Save(page, Path.Combine(output, "stopped-with-next-batch.png"));
        _release = true;
        start.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        PumpUntil(() => type.GetField("_downloadCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page) == null);
        if (Requests["story-0"] != 1 || list.Items.Cast<object>().Any(item => item.GetType().GetProperty("Status")!.GetValue(item)!.ToString() != "Succeeded"))
            throw new InvalidDataException("Continue repeated a success or lost unfinished tasks");
        Save(page, Path.Combine(output, "continued-complete.png"));
        File.WriteAllText(Path.Combine(output, "interaction.json"), JsonSerializer.Serialize(new
        { Stopped = stopped, NextBatchPreserved = true, SourceAndPathStable = true, SuccessNotRepeated = true, Completed = list.Items.Count }));
        host.Close();
    }

    private static void PumpUntil(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("UI operation did not finish");
            Pump();
            Thread.Sleep(5);
        }
        Pump();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static void Save(FrameworkElement page, string path)
    {
        page.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(page.ActualWidth), (int)Math.Ceiling(page.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
