using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class Program
{
    private static object Construct(Type type, params object[] supplied)
    {
        var constructor = type.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select((p, i) => i < supplied.Length ? supplied[i] : p.DefaultValue).ToArray());
    }
    private static FrameworkElement? FindExecuting(DependencyObject parent)
    {
        if (parent is FrameworkElement element && element.GetType().Name == "SuppressionTaskDetails") return element;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { var found = FindExecuting(VisualTreeHelper.GetChild(parent, i)); if (found != null) return found; }
        return null;
    }
    private static void CheckLogWindow(FrameworkElement page)
    {
        var executing = FindExecuting(page) ?? throw new Exception("Missing executing panel");
        var log = (TextBox)executing.FindName("LogTextBox");
        if (!log.IsReadOnly || string.IsNullOrWhiteSpace(log.Text)) throw new Exception("Inline log binding failed");
        var command = (TextBox)executing.FindName("CommandTextBox");
        if (!command.IsReadOnly || string.IsNullOrWhiteSpace(command.Text)) throw new Exception("Command binding failed");
        var model = executing.DataContext;
        var progress = model.GetType().GetProperty("Progress")!.GetValue(model)!;
        if (log.Text != (string)progress.GetType().GetProperty("Log")!.GetValue(progress)!) throw new Exception("Wrong task log");
        if (command.Text != (string)model.GetType().GetProperty("FfmpegCommandDisplay")!.GetValue(model)!) throw new Exception("Wrong task parameters");
        Console.WriteLine("Unified detail component: inline parameters and task-specific read-only logs verified.");
    }
    [STAThread]
    private static void Main(string[] args)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
        Directory.CreateDirectory(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            foreach (var root in new[] { folder, Path.Combine(folder, "libs") })
            { var file = Path.Combine(root, new AssemblyName(e.Name).Name + ".dll"); if (File.Exists(file)) return Assembly.LoadFrom(file); }
            return null;
        };
        var gui = Assembly.LoadFrom(args[0]);
        var wpf = Assembly.LoadFrom(Path.Combine(folder, "Wpf.Ui.dll"));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        if (args.Length > 2 && args[2] == "p1") { P1Checks.Run(gui, args[1]); app.Shutdown(); return; }
        if (args.Length > 2 && args[2] == "always") { AlwaysQueueChecks.Run(gui, args[1]); app.Shutdown(); return; }
        if (args.Length > 2 && args[2] == "overwrite") { OverwriteChecks.Run(gui, wpf, app, args[1]); app.Shutdown(); return; }
        foreach (var theme in new[] { "Light", "Dark" })
        {
            app.Resources.MergedDictionaries.Clear();
            var themes = (ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ThemesDictionary", true)!)!;
            var property = themes.GetType().GetProperty("Theme")!;
            property.SetValue(themes, Enum.Parse(property.PropertyType, theme));
            app.Resources.MergedDictionaries.Add(themes);
            app.Resources.MergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ControlsDictionary", true)!)!);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
            foreach (var name in new[] { "SuppressPage", "Components.SuppressTaskSettings" })
                foreach (var size in new[] { (Width: 1200, Height: 850), (Width: 800, Height: 540) })
                {
                    var view = (FrameworkElement)Activator.CreateInstance(gui.GetType("SekaiToolsGUI.View.Suppress." + name, true)!)!;
                    if (name.Contains("Settings") && args.Length > 2)
                    {
                        var model = view.DataContext;
                        model.GetType().GetProperty("SourceVideo")!.SetValue(model, Path.GetFullPath(args[2]));
                        var limit = DateTime.UtcNow.AddSeconds(15);
                        while ((bool)model.GetType().GetProperty("IsAnalyzing")!.GetValue(model)! && DateTime.UtcNow < limit)
                        { var waitFrame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => waitFrame.Continue = false); Dispatcher.PushFrame(waitFrame); Thread.Sleep(10); }
                        if ((bool)model.GetType().GetProperty("IsAnalyzing")!.GetValue(model)!) throw new Exception("Media analysis timeout");
                        if ((int)model.GetType().GetProperty("SourceFrameCount")!.GetValue(model)! <= 0) throw new Exception("Media analysis failed");
                        Console.WriteLine(model.GetType().GetProperty("MediaDescription")!.GetValue(model));
                    }
                    if (name == "SuppressPage")
                    {
                        var model = view.DataContext;
                        var modelType = model.GetType();
                        var pending = (System.Collections.IList)modelType.GetProperty("PendingJobs")!.GetValue(model)!;
                        var finished = (System.Collections.IList)modelType.GetProperty("CompletedJobs")!.GetValue(model)!;
                        pending.Clear(); finished.Clear();
                        modelType.GetMethod("CompleteResourcePreparation")!.Invoke(model, null);
                        var jobModelType = gui.GetType("SekaiToolsGUI.ViewModel.Suppress.SuppressionJobModel", true)!;
                        var jobType = jobModelType.GetConstructors()[0].GetParameters()[0].ParameterType;
                        var optionsType = jobType.GetConstructors()[0].GetParameters()[0].ParameterType;
                        var encodingType = optionsType.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First().GetParameters()[3].ParameterType;
                        var progressType = jobModelType.GetProperty("Progress")!.PropertyType;
                        var stateType = progressType.GetConstructors().OrderByDescending(x => x.GetParameters().Length).First().GetParameters()[3].ParameterType;
                        object Job(string file) => Construct(jobModelType, Construct(jobType, Construct(optionsType, "E:\\Videos\\" + file, "E:\\Subtitles\\翻译.ass", "E:\\Output\\" + file, Construct(encodingType), 9000, false, 1080)));
                        var current = modelType.GetProperty("RunningJob")!.GetValue(model);
                        if (current != null) jobModelType.GetProperty("Progress")!.SetValue(current, Construct(progressType, 100, 100, 30d, Enum.Parse(stateType, "Completed"), "示例任务"));
                        finished.Clear();
                        var running = Job("剧情视频.mp4"); pending.Add(running); modelType.GetMethod("TryStart")!.Invoke(model, [running]);
                        jobModelType.GetProperty("Progress")!.SetValue(running, Construct(progressType, 1800, 9000, 45d, Enum.Parse(stateType, "Running"), "面板布局示例，未运行编码进程", "3200kbits/s", "0.75x", "128MiB", "00:00:30", 40d, false, "ffmpeg.exe -f yuv4mpegpipe -i - -i source.mp4 -c:v libx264 -preset medium -crf 21 output.partial.mp4 -n"));
                        pending.Add(Job("下一段剧情.mp4"));
                        foreach (var status in new[] { "Completed", "Failed", "Cancelled" })
                        {
                            var item = Job(status + ".mp4");
                            jobModelType.GetProperty("Progress")!.SetValue(item, Construct(progressType, 900, 9000, 45d, Enum.Parse(stateType, status), "布局示例日志"));
                            finished.Add(item);
                        }
                        modelType.GetProperty("CompletedFilter")!.SetValue(model, 2);
                        if (((System.Windows.Data.ListCollectionView)modelType.GetProperty("FilteredCompletedJobs")!.GetValue(model)!).Count != 1) throw new Exception("Failed filter incorrect");
                        modelType.GetProperty("CompletedFilter")!.SetValue(model, 0);
                        modelType.GetProperty("SelectedJob")!.SetValue(model, running);
                        if (!((string)modelType.GetProperty("QueueTimeEstimate")!.GetValue(model)!).Contains("整队约剩")) throw new Exception("Queue estimate missing for matching samples");
                    }
                    FrameworkElement content = name.Contains("Settings") ? new ScrollViewer { Content = view, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } : view;
                    var host = new Window { Width = size.Width, Height = size.Height, Content = content, Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
                    host.SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
                    host.Show();
                    var manager = wpf.GetType("Wpf.Ui.Appearance.ApplicationThemeManager", true)!;
                    var apply = manager.GetMethods().First(method => method.Name == "Apply" && method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType.Name == "ApplicationTheme" && method.GetParameters().Skip(1).All(parameter => parameter.IsOptional));
                    apply.Invoke(null, apply.GetParameters().Select((parameter, index) => index == 0 ? Enum.Parse(parameter.ParameterType, theme) : parameter.DefaultValue).ToArray());
                    host.UpdateLayout();
                    var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false); Dispatcher.PushFrame(frame);
                    content.Measure(new Size(size.Width - 32, size.Height - 48)); content.Arrange(new Rect(0, 0, size.Width - 32, size.Height - 48)); content.UpdateLayout();
                    host.Content = null; var background = new Border { Background = (Brush)host.FindResource("ApplicationBackgroundBrush"), Child = content };
                    host.Content = background;
                    background.Measure(new Size(size.Width - 32, size.Height - 48)); background.Arrange(new Rect(0, 0, size.Width - 32, size.Height - 48)); background.UpdateLayout();
                    var image = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(background);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                    using (var stream = File.Create(Path.Combine(args[1], $"{theme}-{name}-{size.Width}.png"))) encoder.Save(stream);
                    if (name == "SuppressPage")
                    {
                        CheckLogWindow(view);
                        var pageModel = view.DataContext;
                        var pendingItems = (System.Collections.IList)pageModel.GetType().GetProperty("PendingJobs")!.GetValue(pageModel)!;
                        var finishedItems = (System.Collections.IList)pageModel.GetType().GetProperty("CompletedJobs")!.GetValue(pageModel)!;
                        var unifiedDetails = FindExecuting(view);
                        foreach (var selection in new[] { ("Pending", pendingItems[0]), ("Completed", finishedItems[0]), ("Failed", finishedItems[1]) })
                        {
                            pageModel.GetType().GetProperty("SelectedJob")!.SetValue(pageModel, selection.Item2);
                            var detailFrame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => detailFrame.Continue = false); Dispatcher.PushFrame(detailFrame);
                            background.UpdateLayout();
                            if (!ReferenceEquals(unifiedDetails, FindExecuting(view))) throw new Exception("Detail component replaced on task switch");
                            CheckLogWindow(view);
                            var details = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); details.Render(background);
                            var detailEncoder = new PngBitmapEncoder(); detailEncoder.Frames.Add(BitmapFrame.Create(details));
                            using var detailStream = File.Create(Path.Combine(args[1], $"{theme}-SuppressPage-{size.Width}-{selection.Item1}.png")); detailEncoder.Save(detailStream);
                        }
                    }
                    host.Close();
                }
        }
        Console.WriteLine("Rendered 20 panel captures (light/dark, wide/short). No encoding or user settings writes.");
        app.Shutdown();
    }
}

