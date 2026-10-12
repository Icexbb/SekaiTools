using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SekaiToolsGUI.Model;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsInfrastructure.Resources;
using SekaiToolsCore.Process;
using SekaiToolsCore;
using SekaiToolsCore.Process.Config;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is < 1 or > 3) throw new ArgumentException("PeriodicColorSettingsProbe <new-output-directory>");
        var output = Path.GetFullPath(args[0]);
        if (Directory.Exists(output)) throw new IOException("Use a fresh output directory");
        var userSetting = Path.Combine(ResourceManager.DataBaseDir, "Data", "setting.json");
        var before = Hash(userSetting);
        var userStoresBefore = UserStoreFingerprint();
        var application = new Application();
        SynchronizationContext.SetSynchronizationContext(
            new System.Windows.Threading.DispatcherSynchronizationContext(application.Dispatcher));
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, "setting.json");
        var constructor = typeof(SettingPageModel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(string)], null)!;
        SettingPageModel Create() => (SettingPageModel)constructor.Invoke([path]);
        var model = Create();
        if (model.RecognitionPeriodicColor || File.Exists(path)) throw new InvalidDataException("Default enabled or initialization saved settings");
        model.RecognitionPrepareGray = true;
        model.RecognitionPeriodicColor = true;
        var saved = Setting.Load(path);
        if (!saved.RecognitionPeriodicColor || saved.RecognitionPrepareGray) throw new InvalidDataException("Conflicting options persisted");
        var restored = Create();
        if (!restored.RecognitionPeriodicColor || restored.RecognitionPrepareGray) throw new InvalidDataException("Enabled option did not reload");
        restored.RecognitionPrepareGray = true;
        saved = Setting.Load(path);
        if (saved.RecognitionPeriodicColor || !saved.RecognitionPrepareGray) throw new InvalidDataException("Gray selection did not disable periodic mode");
        restored.ResetSetting();
        if (Setting.Load(path).RecognitionPeriodicColor) throw new InvalidDataException("Reset enabled periodic mode");
        File.WriteAllText(path, "{\"RecognitionPrepareGray\":true}");
        var legacy = Create();
        if (legacy.RecognitionPeriodicColor || !legacy.RecognitionPrepareGray) throw new InvalidDataException("Legacy defaults changed");
        VerifySavedCaptureSelection();
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary());
        application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
            { Source = new Uri("/SekaiToolsGUI;component/Converter/Converters.xaml", UriKind.Relative) });
        var page = new SekaiToolsGUI.View.Setting.SettingPage { DataContext = legacy };
        var nodes = Descendants(page).ToArray();
        foreach (var expander in nodes.OfType<Wpf.Ui.Controls.CardExpander>()) expander.IsExpanded = true;
        page.Measure(new Size(1000, 900));
        page.Arrange(new Rect(0, 0, 1000, 900));
        page.UpdateLayout();
        nodes = Descendants(page).ToArray();
        foreach (var expander in nodes.OfType<Wpf.Ui.Controls.CardExpander>()) expander.IsExpanded = true;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        page.UpdateLayout();
        var checkbox = nodes.OfType<CheckBox>().Single(c =>
            BindingOperations.GetBinding(c, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.Path?.Path == "RecognitionPeriodicColor");
        checkbox.IsChecked = true;
        checkbox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)!.UpdateSource();
        if (!legacy.RecognitionPeriodicColor || Setting.Load(path).RecognitionPrepareGray)
            throw new InvalidDataException("Actual settings checkbox binding failed");
        var scroll = nodes.OfType<ScrollViewer>().First();
        scroll.ScrollToVerticalOffset(checkbox.TranslatePoint(new Point(), scroll).Y - 300);
        page.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1000, 900, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(page);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, "settings.png"))) encoder.Save(stream);
        VerifyMainWindowClose(application, args.Length >= 2 ? Path.GetFullPath(args[1]) : null, output, args.Length == 3);
        if (UserStoreFingerprint() != userStoresBefore) throw new InvalidDataException("User persistence stores changed");
        if (Hash(userSetting) != before) throw new InvalidDataException("User settings changed");
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            VerificationPassed = true, DefaultDisabled = true, MutualExclusionPersisted = true,
            EnabledSettingReloaded = true, ResetDisabled = true, LegacyDefaultsPreserved = true,
            UserSettingsUnchanged = true, ActualSettingsPageBindingQualified = true,
            OffscreenSettingsPageRendered = true, LiveGuiQualified = false,
            SavedCaptureSelectionChecked = true, UnknownCaptureIdentityRejected = true,
            NativeMainWindowSettingsNavigationAndCloseQualified = true, ActiveQueueWindowCloseQualified = args.Length == 3,
            ActualTaskPreviewToggleAndCancelQualified = args.Length >= 2,
            PeriodicQueuePreviewToggleQualified = args.Length == 3,
            ProductFactoryFailureRecoveryQualified = args.Length == 3,
            UserPersistenceStoresUnchanged = true,
            NativeHash = args.Length >= 2 ? Hash(System.Diagnostics.Process.GetCurrentProcess().Modules
                .Cast<System.Diagnostics.ProcessModule>().Single(m => m.ModuleName.Equals("cvextern.dll", StringComparison.OrdinalIgnoreCase)).FileName) : null,
            GuiAssemblyHash = Hash(typeof(SettingPageModel).Assembly.Location)
        }, new JsonSerializerOptions { WriteIndented = true }));
        application.Shutdown();
    }

    private static void VerifyMainWindowClose(Application application, string? video, string output, bool queue)
    {
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var constructor = typeof(SekaiToolsGUI.MainWindow).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(bool)], null)!;
        var window = (SekaiToolsGUI.MainWindow)constructor.Invoke([false]);
        // No page initialization, downloads or saved-progress restoration in this
        // empty-window check. Production construction retains normal navigation.
        var model = (SekaiToolsGUI.ViewModel.MainWindowViewModel)window.DataContext;
        model.NavigationItems = [];
        window.DataContext = null;
        window.DataContext = model;
        window.ShowInTaskbar = false;
        window.ShowActivated = false;
        window.Opacity = 0;
        application.MainWindow = window;
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();
        if (new System.Windows.Interop.WindowInteropHelper(window).Handle == IntPtr.Zero)
            throw new InvalidDataException("Main window has no native handle");
        var navigation = (Wpf.Ui.Controls.NavigationView)window.FindName("NavigationView");
        if (!navigation.Navigate(typeof(SekaiToolsGUI.View.Setting.SettingPage)))
            throw new InvalidDataException("Actual main window settings navigation failed");
        var currentPage = typeof(SekaiToolsGUI.MainWindow).GetField("_currentNavigatedPage",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
        if (currentPage is not SekaiToolsGUI.View.Setting.SettingPage)
            throw new InvalidDataException("Main window did not receive settings navigation event");
        if (video != null) VerifyActiveTask(video, output);
        var verifyQueue = queue ? PrepareActiveQueue(window, video!, output) : null;
        window.Close();
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        var deadline = DateTime.UtcNow.AddSeconds(10);
        timer.Tick += (_, _) =>
        {
            if (closed || DateTime.UtcNow >= deadline) { timer.Stop(); frame.Continue = false; }
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        if (!closed) throw new InvalidDataException("Main window did not complete asynchronous closing");
        verifyQueue?.Invoke();
    }

    private static Action PrepareActiveQueue(SekaiToolsGUI.MainWindow window, string video, string output)
    {
        VerifyCaptureFailureRecovery(video, output);
        var settingsConstructor = typeof(SettingPageModel).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, [typeof(string)], null)!;
        var settings = (SettingPageModel)settingsConstructor.Invoke([Path.Combine(output, "queue-settings.json")]);
        settings.RecognitionPeriodicColor = true;
        var page = new SekaiToolsGUI.View.Subtitle.SubtitlePage();
        window.Content = page;
        var videos = new[] { video }.Concat(Directory.EnumerateFiles(Path.GetDirectoryName(video)!, "*.mp4", SearchOption.AllDirectories)
            .Where(path => !path.Equals(video, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.ChangeExtension(path, ".json")) && File.Exists(Path.ChangeExtension(path, ".txt"))))
            .Take(3).ToArray();
        if (videos.Length != 3) throw new InvalidDataException("Three standard video groups required");
        var tasks = new List<SekaiToolsGUI.ViewModel.Subtitle.SubtitleQueueTaskModel>();
        var stores = new List<IsolatedPersistence>();
        var type = typeof(SekaiToolsGUI.View.Subtitle.Components.SubtitleTask);
        var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(SekaiToolsGUI.ViewModel.Subtitle.SubtitlePageModel), typeof(SekaiToolsCore.Abstractions.IProcessingStatePersistence), typeof(SettingPageModel)], null)!;
        foreach (var path in videos)
        {
            var state = new SekaiToolsGUI.ViewModel.Subtitle.SubtitlePageModel
            { VideoFilePath = path, ScriptFilePath = Path.ChangeExtension(path, ".json"), TranslateFilePath = Path.ChangeExtension(path, ".txt") };
            var task = new SekaiToolsGUI.ViewModel.Subtitle.SubtitleQueueTaskModel(state);
            var store = new IsolatedPersistence(Path.Combine(output, $"queue-{tasks.Count}.json"));
            var control = constructor.Invoke([state, store, settings]);
            task.GetType().GetField("_control", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(task, control);
            tasks.Add(task); stores.Add(store);
            page.GetType().GetMethod("AddTask", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [task]);
        }
        var initialImage = tasks[0].State.FramePreviewImage;
        PumpUntil(() => !ReferenceEquals(initialImage, tasks[0].State.FramePreviewImage));
        var processor = (VideoProcessor)type.GetProperty("VideoProcessor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tasks[0].Control)!;
        var frameField = typeof(VideoProcessor).GetField("_processedFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var firstFrame = (int)frameField.GetValue(processor)!;
        var pausedImage = tasks[0].State.FramePreviewImage;
        tasks[0].State.ShowPreview = false;
        PumpUntil(() => (int)frameField.GetValue(processor)! >= firstFrame + 100 || !tasks[0].State.IsRunning);
        if (!tasks[0].State.IsRunning || !ReferenceEquals(pausedImage, tasks[0].State.FramePreviewImage))
            throw new InvalidDataException("Periodic queue preview did not pause while processing continued");
        tasks[0].State.ShowPreview = true;
        PumpUntil(() => !ReferenceEquals(pausedImage, tasks[0].State.FramePreviewImage) || !tasks[0].State.IsRunning);
        if (!tasks[0].State.IsRunning || ReferenceEquals(pausedImage, tasks[0].State.FramePreviewImage))
            throw new InvalidDataException("Periodic queue preview did not resume");
        type.GetMethod("RequestStop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tasks[0].Control, null);
        var secondImage = tasks[1].State.FramePreviewImage;
        PumpUntil(() => tasks[0].State.IsCanceled && tasks[1].State.IsRunning && !ReferenceEquals(secondImage, tasks[1].State.FramePreviewImage));
        var firstHash = Hash(Path.Combine(output, "queue-0.json"));
        page.GetType().GetMethod("SelectTask", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [tasks[0]]);
        if (!ReferenceEquals(page.ViewModel.SelectedTask, tasks[0])) throw new InvalidDataException("Actual queue selection failed");
        return () =>
        {
            if (!tasks[1].State.IsCanceled || !tasks[2].State.HasNotStarted || stores[2].State != null)
                throw new InvalidDataException("Window closing advanced queue or did not cancel active task");
            if (firstHash != Hash(Path.Combine(output, "queue-0.json"))) throw new InvalidDataException("Next task overwrote canceled checkpoint");
            foreach (var store in stores.Take(2))
                if (store.State?.StopReason != ProcessStopReason.Canceled || store.State.Metadata?.CaptureIdentity?.StartsWith("PeriodicColor:") != true)
                    throw new InvalidDataException("Periodic queue checkpoint missing identity");
            foreach (var task in tasks)
                if (type.GetProperty("VideoProcessor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(task.Control) != null)
                    throw new InvalidDataException("Closed queue retained a processor");
        };
    }

    private static void VerifyCaptureFailureRecovery(string video, string output)
    {
        var factory = SekaiToolsInfrastructure.Video.PeriodicColorCaptureFactory.ForCurrentRuntime();
        ExpectFailure<ArgumentOutOfRangeException>(() => factory.Open(video, 0));
        ExpectFailure<ArgumentOutOfRangeException>(() => factory.Open(video, 1000001));
        var missingVideo = Path.Combine(output, "missing-video.mp4");
        if (File.Exists(missingVideo)) throw new InvalidDataException("Missing video fixture already exists");
        var wrongPlugin = Path.Combine(output, "wrong-plugin.dll");
        File.WriteAllBytes(wrongPlugin, [0, 1, 2, 3]);
        var invalidFactory = new SekaiToolsInfrastructure.Video.PeriodicColorCaptureFactory(wrongPlugin);
        ExpectFailure<InvalidDataException>(() => invalidFactory.Open(video, 12));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            ExpectFailure<InvalidOperationException>(() => factory.Open(missingVideo, 12));
            using var session = factory.Open(video, 12);
            using var image = new Emgu.CV.Mat();
            for (var frame = 1; frame <= 13; frame++)
            {
                if (!session.Capture.Read(image) || image.IsEmpty || image.NumberOfChannels != (frame % 12 == 0 ? 3 : 1))
                    throw new InvalidDataException("Factory did not recover after failed construction");
            }
            session.Dispose();
            session.Dispose();
            if (session.Capture.Ptr != IntPtr.Zero) throw new InvalidDataException("Recovered capture did not release");
        }
    }

    private static void ExpectFailure<T>(Func<SekaiToolsCore.Abstractions.IVideoCaptureSession> open) where T : Exception
    {
        try { using var session = open(); }
        catch (T) { return; }
        throw new InvalidDataException($"Factory did not reject with {typeof(T).Name}");
    }

    private static void VerifyActiveTask(string video, string output)
    {
        var model = new SekaiToolsGUI.ViewModel.Subtitle.SubtitlePageModel
        {
            VideoFilePath = video, ScriptFilePath = Path.ChangeExtension(video, ".json"),
            TranslateFilePath = Path.ChangeExtension(video, ".txt"), ShowPreview = true
        };
        var persistence = new IsolatedPersistence(Path.Combine(output, "task-progress.json"));
        var type = typeof(SekaiToolsGUI.View.Subtitle.Components.SubtitleTask);
        var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [model.GetType(), typeof(SekaiToolsCore.Abstractions.IProcessingStatePersistence)], null)!;
        var control = (SekaiToolsGUI.View.Subtitle.Components.SubtitleTask)constructor.Invoke([model, persistence]);
        var previewCount = 0;
        model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.FramePreviewImage)) previewCount++; };
        var running = (Task)type.GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [null])!;
        PumpUntil(() => previewCount >= 2 || running.IsCompleted);
        if (running.IsCompleted || previewCount < 2) throw new InvalidDataException("Active task did not deliver previews");
        var processor = (SekaiToolsCore.VideoProcessor)type.GetProperty("VideoProcessor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control)!;
        var frameField = typeof(SekaiToolsCore.VideoProcessor).GetField("_processedFrameIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var firstFrame = (int)frameField.GetValue(processor)!;
        var beforeDisable = previewCount;
        model.ShowPreview = false;
        PumpUntil(() => (int)frameField.GetValue(processor)! >= firstFrame + 100 || running.IsCompleted);
        if (running.IsCompleted || previewCount != beforeDisable) throw new InvalidDataException("Disabled preview changed or task ended too early");
        model.ShowPreview = true;
        PumpUntil(() => previewCount > beforeDisable || running.IsCompleted);
        if (running.IsCompleted || previewCount <= beforeDisable) throw new InvalidDataException("Preview did not resume");
        type.GetMethod("RequestStop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null);
        PumpUntil(() => running.IsCompleted);
        running.GetAwaiter().GetResult();
        if (!model.IsCanceled || persistence.State?.StopReason != ProcessStopReason.Canceled)
            throw new InvalidDataException("GUI cancellation did not save canceled state");
        var disposal = (Task)type.GetMethod("DisposeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, null)!;
        PumpUntil(() => disposal.IsCompleted);
        disposal.GetAwaiter().GetResult();
        if (type.GetProperty("VideoProcessor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control) != null)
            throw new InvalidDataException("GUI disposal retained processor");
    }

    private static void PumpUntil(Func<bool> done)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => { if (done() || DateTime.UtcNow >= deadline) { timer.Stop(); frame.Continue = false; } };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        if (!done()) throw new TimeoutException("GUI task probe timed out");
    }

    private sealed class IsolatedPersistence(string path) : SekaiToolsCore.Abstractions.IProcessingStatePersistence
    {
        private static readonly Action<string, ProcessingState> Save = typeof(SekaiToolsInfrastructure.Persistence.ProgressStore)
            .GetMethod("SaveToPath", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Action<string, ProcessingState>>();
        public ProcessingState? State { get; private set; }
        public void SaveProgress(string key, ProcessingState state)
        {
            Save(path, state);
            State = JsonSerializer.Deserialize<ProcessingState>(File.ReadAllText(path));
        }
        public void DeleteProgress(string key) => throw new InvalidOperationException("Probe must retain progress");
        public void AddHistory(ProcessingState state) => throw new InvalidOperationException("Cancellation probe must not finish");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private static void VerifySavedCaptureSelection()
    {
        var type = typeof(SekaiToolsGUI.View.Subtitle.Components.SubtitleTask);
        var choose = type.GetMethod("GetCaptureFactory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var options = type.GetMethod("GetRecognitionOptions", BindingFlags.Static | BindingFlags.NonPublic)!;
        var ordinary = new ProcessingState();
        if (choose.Invoke(null, [ordinary]) != null) throw new InvalidDataException("Ordinary saved task changed capture mode");
        var fingerprint = new FileFingerprint(0, 0, "fixture");
        var periodic = new ProcessingState { Metadata = new ProcessingStateMetadata(fingerprint, fingerprint, null,
            new VideoStateMetadata(1920, 1080, 100, 60), "fixture") { CaptureIdentity = "PeriodicColor:fixture" } };
        if (((RecognitionPerformanceOptions)options.Invoke(null, [periodic])!).PrepareGrayFrames)
            throw new InvalidDataException("Periodic saved task enabled BGR-only preprocessing");
        periodic.Metadata = periodic.Metadata with { CaptureIdentity = "UnknownCapture:fixture" };
        try { choose.Invoke(null, [periodic]); }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException) { return; }
        throw new InvalidDataException("Unknown capture identity accepted");
    }

    private static string UserStoreFingerprint()
    {
        var entries = new SortedDictionary<string, string?>();
        foreach (var directory in new[] { "Data", "Progress", "SubtitleQueueProgress", "History" })
        {
            var path = Path.Combine(ResourceManager.DataBaseDir, directory);
            if (!Directory.Exists(path)) { entries[path] = null; continue; }
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) entries[file] = Hash(file);
        }
        return JsonSerializer.Serialize(entries);
    }

    private static string? Hash(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;
}
