using System.IO;
using System.Collections;
using System.Windows.Automation;
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
        if (args.Length is < 2 or > 3) throw new ArgumentException("DownloadUxProbe <GUI dll> <new output directory> [browser]");
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
        if (args.Length == 3)
        {
            if (args[2] != "browser") throw new ArgumentException("Unknown probe mode");
            VerifyBrowsers(gui, data, wpf, app, output);
            return;
        }
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
                        ["活动剧情 · 较长标题用于检查换行和操作按钮是否被遮挡 · 第 12 章", "https://download.invalid/" + state + ".json", "测试数据源", output])!;
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
                if (compact)
                {
                    Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("QueueDetailsButton"));
                    Pump();
                }
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

    private static void VerifyBrowsers(Assembly gui, Assembly data, Assembly wpf, Application app, string output)
    {
        var domain = Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(gui.Location)!, "SekaiToolsBase.dll"));
        object Parse(string name, string json) => JsonSerializer.Deserialize(json, domain.GetType("SekaiToolsBase.DataList." + name, true)!)!;
        object Make(string name, params object[] values) => Activator.CreateInstance(data.GetType("SekaiDataFetch.Item." + name, true)!, values)!;
        object Collection(string name)
        {
            var type = data.GetType("SekaiDataFetch.List.List" + name + "Story", true)!;
            var instance = type.GetProperty("Instance")!.GetValue(null);
            return type.GetField("Data")?.GetValue(instance) ?? type.GetProperty("Data")!.GetValue(instance)!;
        }
        var eventList = (IList)Collection("Event");
        eventList.Clear();
        eventList.Add(Make("EventStorySet", Parse("EventStory", "{\"EventId\":777,\"AssetBundleName\":\"event\",\"EventStoryEpisodes\":[{\"EpisodeNo\":1,\"Title\":\"Alpha\",\"ScenarioId\":\"probe_alpha\"},{\"EpisodeNo\":2,\"Title\":\"Beta\",\"ScenarioId\":\"probe_beta\"}]}"),
            Parse("GameEvent", "{\"Name\":\"Sky festival\",\"EventType\":\"marathon\"}"), 42, new[] { 1 }));
        var cards = (IList)Collection("Card");
        cards.Clear();
        cards.Add(Make("CardStorySet", Parse("Card", "{\"Id\":42,\"CharacterId\":1,\"CardRarityType\":\"rarity_4\",\"Prefix\":\"Sky card\"}"),
            Parse("CardEpisode", "{\"ScenarioId\":\"probe_alpha\",\"AssetBundleName\":\"alpha\"}"),
            Parse("CardEpisode", "{\"ScenarioId\":\"probe_beta\",\"AssetBundleName\":\"beta\"}")));
        var units = (IDictionary)Collection("Unit");
        units.Clear();
        units.Add("light_sound", Make("UnitStorySet", Parse("UnitStory", "{\"Unit\":\"light_sound\",\"Chapters\":[{\"Title\":\"Sky chapter\",\"AssetBundleName\":\"unit\",\"Episodes\":[{\"Title\":\"Alpha\",\"EpisodeNoLabel\":\"1\",\"ScenarioId\":\"probe_alpha\"},{\"Title\":\"Beta\",\"EpisodeNoLabel\":\"2\",\"ScenarioId\":\"probe_beta\"}]}]}")));
        var specials = (IDictionary)Collection("Special");
        specials.Clear();
        specials.Add("Sky special", Make("SpecialStorySet", Parse("SpecialStory", "{\"Title\":\"Sky special\",\"AssetBundleName\":\"special\",\"Episodes\":[{\"Title\":\"Alpha\",\"AssetBundleName\":\"alpha\",\"ScenarioId\":\"probe_alpha\"},{\"Title\":\"Beta\",\"AssetBundleName\":\"beta\",\"ScenarioId\":\"probe_beta\"}]}")));
        var actions = (IList)Collection("Action");
        actions.Clear();
        for (var index = 0; index < 2; index++)
        {
            var item = Make("AreaStorySet", Parse("ActionSet", "{\"Id\":" + (100 + index) + ",\"AreaId\":1,\"ActionSetType\":\"normal\",\"ScenarioId\":\"probe_" + (index == 0 ? "alpha" : "beta") + "\",\"ScriptId\":\"probe_" + (index == 0 ? "alpha" : "beta") + "\"}"));
            item.GetType().GetProperty("TalkId")!.SetValue(item, "000" + (index + 1));
            actions.Add(item);
        }
        var rows = new List<object>();
        foreach (var theme in new[] { "Light", "Dark" })
        {
            app.Resources.MergedDictionaries.Clear();
            var themes = (ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ThemesDictionary", true)!)!;
            var themeProperty = themes.GetType().GetProperty("Theme")!;
            themeProperty.SetValue(themes, Enum.Parse(themeProperty.PropertyType, theme));
            app.Resources.MergedDictionaries.Add(themes);
            app.Resources.MergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(wpf.GetType("Wpf.Ui.Markup.ControlsDictionary", true)!)!);
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
            // Real application loads settings once, then keeps a theme while browsing categories.
            _ = gui.GetType("SekaiToolsGUI.ViewModel.Setting.SettingPageModel", true)!.GetProperty("Instance")!.GetValue(null);
            var manager = wpf.GetType("Wpf.Ui.Appearance.ApplicationThemeManager", true)!;
            var apply = manager.GetMethods().First(method => method.Name == "Apply" && method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType.Name == "ApplicationTheme" && method.GetParameters().Skip(1).All(parameter => parameter.IsOptional));
            apply.Invoke(null, apply.GetParameters().Select((parameter, index) => index == 0 ? Enum.Parse(parameter.ParameterType, theme) : parameter.DefaultValue).ToArray());
            foreach (var category in new[] { "Unit", "Event", "Special", "Card", "Action" })
            {
                var pageType = gui.GetType("SekaiToolsGUI.View.Download.DownloadPage", true)!;
                var page = (FrameworkElement)pageType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(bool)], null)!.Invoke([false]);
                var categoryBox = (System.Windows.Controls.Primitives.Selector)page.FindName("BoxStoryType");
                var categoryHandler = (SelectionChangedEventHandler)Delegate.CreateDelegate(typeof(SelectionChangedEventHandler), page,
                    pageType.GetMethod("StoryTypeSelector_OnSelected", BindingFlags.Instance | BindingFlags.NonPublic)!);
                categoryBox.RemoveHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, categoryHandler);
                categoryBox.SelectedIndex = Array.IndexOf(new[] { "Unit", "Event", "Special", "Card", "Action" }, category);
                var sourceBox = (System.Windows.Controls.Primitives.Selector)page.FindName("BoxSource");
                var sourceHandler = (SelectionChangedEventHandler)Delegate.CreateDelegate(typeof(SelectionChangedEventHandler), page,
                    pageType.GetMethod("SourceSelector_OnSelected", BindingFlags.Instance | BindingFlags.NonPublic)!);
                sourceBox.RemoveHandler(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, sourceHandler);
                var tab = (UserControl)Activator.CreateInstance(gui.GetType($"SekaiToolsGUI.View.Download.Components.{category}.{category}StoryTab", true)!)!;
                var hook = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), page, pageType.GetMethod("Browser_OnResultsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!);
                tab.GetType().GetEvent("ResultsChanged")!.AddEventHandler(tab, hook);
                ((ContentControl)page.FindName("ContentCard")).Content = tab;
                var host = new Window { Content = page, Width = category == "Action" ? 960 : 1280, Height = 800, Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
                app.MainWindow = host;
                host.SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
                host.Show();
                Pump();
                var search = (TextBox)page.FindName("SearchBox");
                var refresh = pageType.GetMethod("ApplyBrowserSearch", BindingFlags.Instance | BindingFlags.NonPublic)!;
                int Count() => ((IEnumerable)tab.GetType().GetProperty("VisibleDownloads")!.GetValue(tab)!).Cast<object>().Count();
                void Query(string query) { search.Text = query; refresh.Invoke(page, null); Pump(); }
                pageType.GetMethod("UpdateTaskListState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
                Query("");
                if (Count() != 2) throw new InvalidDataException(category + " initial file-level count differs: " + Count());
                if (category is "Event" or "Card" or "Unit")
                {
                    var filterName = category == "Event" ? "BoxType" : category == "Card" ? "RarityComboBox" : "UnitComboBox";
                    ((System.Windows.Controls.Primitives.Selector)tab.FindName(filterName)).SelectedIndex = category == "Event" ? 2 : 1;
                    Pump();
                    if (Count() != 0) throw new InvalidDataException(category + " native filter did not intersect results");
                    Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("ResetSearchButton"));
                    Pump();
                    if (Count() != 2) throw new InvalidDataException(category + " reset lost native filter defaults");
                }
                if (category == "Action")
                {
                    ((System.Windows.Controls.Primitives.Selector)tab.FindName("IdIndexBox")).SelectedIndex = 1;
                    ((TextBox)tab.FindName("MinimumIdBox")).Text = "0002";
                    Pump();
                    if (Count() != 1) throw new InvalidDataException("TalkId filter was not preserved");
                    Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("ResetSearchButton"));
                    Pump();
                    if (Count() != 2) throw new InvalidDataException("TalkId reset did not restore files");
                }
                for (var stress = 0; stress < 10; stress++) { Query("probe_alpha"); Query("probe_beta"); }
                Query("");
                Save(page, Path.Combine(output, $"{theme}-{category}-results.png"));
                Query("probe_alpha");
                var selectAll = (CheckBox)page.FindName("SelectResultsBox");
                selectAll.IsChecked = true;
                Click(selectAll);
                if (!((FrameworkElement)page.FindName("AddSelectedButton")).IsEnabled) throw new InvalidDataException("Selection did not enable add");
                Query("probe_beta");
                if (Count() != 1 || ((FrameworkElement)page.FindName("AddSelectedButton")).IsEnabled) throw new InvalidDataException("Hidden selection survived search change");
                var boxes = Nodes(page).OfType<CheckBox>().Where(box => box.IsVisible && AutomationProperties.GetName(box).StartsWith("选择下载项 ", StringComparison.Ordinal)).ToArray();
                if (boxes.Length != 1) throw new InvalidDataException(category + " visible item checkboxes differ: " + boxes.Length);
                foreach (var item in Nodes(page).OfType<FrameworkElement>().Where(item => item.GetType().Name == "DownloadItem" && item.IsVisible))
                    if (((TextBlock)item.FindName("TitleTextBlock")).ActualWidth < 32) throw new InvalidDataException("Chapter title was squeezed by selection state");
                boxes[0].IsChecked = true;
                Click(boxes[0]);
                Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("AddSelectedButton"));
                Pump();
                var queue = (ItemsControl)page.FindName("DownloadItemBox");
                if (queue.Items.Count != 1 || boxes[0].IsEnabled || !Equals(boxes[0].Content, "已加入")) throw new InvalidDataException("Queue state was not reflected on list");
                Save(page, Path.Combine(output, $"{theme}-{category}-selected.png"));
                Query("no_matching_story_xyz");
                if (Count() != 0 || !((FrameworkElement)page.FindName("BrowserEmptyPanel")).IsVisible) throw new InvalidDataException("No-result state missing");
                Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("ResetSearchButton"));
                Pump();
                if (Count() != 2 || search.Text.Length != 0) throw new InvalidDataException("Reset did not restore results");
                selectAll.IsChecked = true;
                Click(selectAll);
                Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("AddSelectedButton"));
                Pump();
                if (queue.Items.Count != 2) throw new InvalidDataException("Batch selection duplicated or missed a result");
                rows.Add(new { Theme = theme, Category = category, InitialFiles = 2, SearchFiles = 1, RapidSearchCycles = 10, HiddenSelectionCleared = true, QueuedFiles = queue.Items.Count, QueueStatusVisible = true });
                host.Close();
            }
            PumpFor(TimeSpan.FromMilliseconds(400));
        }
        File.WriteAllText(Path.Combine(output, "browser-report.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PASS five real browsers: search, counts, per-item and all-result selection, hidden selection clearing, queue status, empty and reset states in both themes");
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            foreach (var child in Nodes(VisualTreeHelper.GetChild(node, index))) yield return child;
    }
    private static void Click(System.Windows.Controls.Primitives.ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

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
        for (var resize = 0; resize < 3; resize++)
        {
            host.Width = 800;
            Pump();
            if (!start.IsVisible || ((FrameworkElement)page.FindName("QueueBody")).IsVisible) throw new InvalidDataException("Compact queue summary failed during a batch");
            Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("QueueDetailsButton"));
            Pump();
            if (!((FrameworkElement)page.FindName("QueueBody")).IsVisible) throw new InvalidDataException("Queue details did not expand during a batch");
            Click((System.Windows.Controls.Primitives.ButtonBase)page.FindName("QueueDetailsButton"));
            host.Width = 1280;
            Pump();
        }
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
        PumpFor(TimeSpan.FromMilliseconds(250));
        page.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(page.ActualWidth), (int)Math.Ceiling(page.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
