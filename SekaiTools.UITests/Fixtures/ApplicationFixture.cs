using System.Collections.Concurrent;
using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using SekaiTools.UITests.Automation;
using SekaiTools.UITests.Helpers;
using SekaiToolsConfiguration;

namespace SekaiTools.UITests.Fixtures;

public sealed class ApplicationFixture : IDisposable
{
    private readonly ConcurrentQueue<string> _output = new();

    internal string? FindOutputLine(Func<string, bool> predicate) => _output.FirstOrDefault(predicate);
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<object> _steps = [];
    private Process? _process;
    private Application? _application;
    private UIA3Automation? _automation;
    private bool _failureSaved;
    private bool _disposed;
    private string _cleanup = "未关闭";
    private object? _environment;
    private Exception? _failure;

    public ApplicationFixture(string testId, string category = "Smoke")
    {
        TestId = testId;
        Category = category;
        Data = new TestDataManager(testId);
    }

    public string TestId { get; }
    public string Category { get; }
    public TestDataManager Data { get; }
    public ElementFinder Finder { get; } = new();
    public Window? Window { get; private set; }

    public void Step(string name, string expected, Action action)
    {
        var started = _watch.Elapsed;
        try
        {
            action();
            _steps.Add(new
            {
                Name = name,
                Expected = expected,
                Actual = expected,
                Result = "Passed",
                DurationMs = (_watch.Elapsed - started).TotalMilliseconds
            });
        }
        catch (Exception e)
        {
            _steps.Add(new
            {
                Name = name,
                Expected = expected,
                Actual = e.Message,
                Result = "Failed",
                DurationMs = (_watch.Elapsed - started).TotalMilliseconds
            });
            RecordFailure(e);
            throw;
        }
    }

    public void Start() => Start(Catalog.Translation);

    public void Start(string expectedPageRoot)
    {
        _environment = TestEnvironment.Verify();
        var executable = Environment.GetEnvironmentVariable("SEKAI_TOOLS_UI_EXE")
            ?? throw new InvalidOperationException("必须指定 SEKAI_TOOLS_UI_EXE。");
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new FileNotFoundException("测试应用 exe 不存在或不是绝对路径。", executable);
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        info.Environment[ApplicationPaths.TestRootVariable] = Data.Root;
        info.Environment["TEMP"] = Data.Temp;
        info.Environment["TMP"] = Data.Temp;
        info.Environment["SEKAI_TOOLS_SKIP_UPDATE"] = "1";
        _process = Process.Start(info) ?? throw new InvalidOperationException("无法创建测试进程。");
        _process.OutputDataReceived += (_, e) => { if (e.Data is { } line) _output.Enqueue(line); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data is { } line) _output.Enqueue(line); };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _application = Application.Attach(_process);
        _automation = new UIA3Automation { ConnectionTimeout = TimeSpan.FromSeconds(3), TransactionTimeout = TimeSpan.FromSeconds(3) };
        Window = WaitHelper.Until(() =>
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"应用提前退出：{_process.ExitCode}\n{string.Join('\n', _output)}");
            return _application.GetAllTopLevelWindows(_automation).FirstOrDefault(window => window.Properties.AutomationId.ValueOrDefault == Catalog.MainWindow);
        }, "真实主窗口就绪", TimeSpan.FromSeconds(30));
        Window.SetForeground();
        Finder.ById(Window, expectedPageRoot);
        if (expectedPageRoot == Catalog.Translation) Finder.ById(Window, Catalog.LoadScript);
        using (var resolved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Data.Root, "resolved-paths.json"))))
        {
            foreach (var key in new[] { "ResourceData", "DownloadData", "VoiceCache", "Temporary" })
                if (!ApplicationPaths.ContainsPath(Data.Root, resolved.RootElement.GetProperty(key).GetString()!))
                    throw new InvalidOperationException($"应用实际路径未隔离：{key}");
        }
        Data.WriteJson("environment", new
        {
            Environment = _environment,
            Executable = executable,
            ProcessId = _process.Id,
            StartTime = _process.StartTime.ToUniversalTime(),
            Window = Window.BoundingRectangle.ToString(),
            Data.Paths.DataRoot,
            Data.Paths.LocalRoot,
            TemporaryRoot = Data.Temp,
            Data.Before,
            TestId,
            Category,
            InitialPage = expectedPageRoot,
            Scope = "Isolated desktop scenario; executed steps and results are recorded in report.json"
        });
    }

    public void Navigate(string navigationId, string pageId)
    {
        Window!.SetForeground();
        ScreenshotHelper.CaptureStable(Window, Path.Combine(Data.Directory, "Screenshots"), "BeforeNavigation");
        var item = Finder.ById(Window!, navigationId);
        // WPF-UI 4.3 exposes these as data-item peers without Invoke/SelectionItem.
        // Use the located item's keyboard activation; no fixed screen coordinates.
        WaitHelper.Until(() => item.IsEnabled && !item.IsOffscreen, $"导航项可操作：{navigationId}");
        item.Focus();
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Finder.ById(Window!, pageId);
        WaitHelper.Until(() => !Finder.ById(Window!, pageId).IsOffscreen, $"页面可见：{pageId}");
        WaitHelper.Until(() => item.Properties.ItemStatus.ValueOrDefault == "当前页面", "导航选中态与页面一致");
        foreach (var otherId in new[] { Catalog.SubtitleNavigation, Catalog.TranslationNavigation,
            Catalog.DownloadNavigation, Catalog.EncodingNavigation, Catalog.SettingsNavigation }.Where(id => id != navigationId))
            if (Finder.ById(Window!, otherId).Properties.ItemStatus.ValueOrDefault == "当前页面")
                throw new InvalidOperationException($"多个导航项同时选中：{navigationId} / {otherId}");
    }

    public void Close()
    {
        if (_process is null) return;
        if (!_process.HasExited) Window?.Close();
        WaitForExit();
    }

    public void WaitForExit()
    {
        if (_process is null) return;
        WaitHelper.Until(() => _process.HasExited, "真实关闭流程完成", TimeSpan.FromSeconds(20));
        _process.WaitForExit();
        if (_process.ExitCode != 0) throw new InvalidOperationException($"应用异常退出：{_process.ExitCode}");
        _cleanup = "正常退出，无自有进程残留";
    }

    public void Restart(string expectedPageRoot = Catalog.Translation)
    {
        Close();
        _application?.Dispose();
        _automation?.Dispose();
        _process?.Dispose();
        _application = null;
        _automation = null;
        _process = null;
        Window = null;
        Start(expectedPageRoot);
    }

    public Window OwnedWindow(string automationId) => WaitHelper.Until(() =>
        Window!.ModalWindows.FirstOrDefault(window => window.Properties.AutomationId.ValueOrDefault == automationId)
        ?? Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId).And(cf.ByProcessId(_process!.Id)))?.AsWindow()
        ?? _application!.GetAllTopLevelWindows(_automation!).FirstOrDefault(window => window.Properties.AutomationId.ValueOrDefault == automationId),
        $"自有进程窗口：{automationId}");

    public Window OwnedFileDialog() => WaitHelper.Until(() =>
        Window!.ModalWindows.FirstOrDefault(window => window.FindFirstDescendant(cf =>
            cf.ByAutomationId("1148").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit))) is not null),
        "自有进程的原生文件选择窗口");

    public void RecordFailure(Exception exception)
    {
        _failure ??= exception;
        if (_failureSaved) return;
        _failureSaved = true;
        if (Window is null && _application is not null && _automation is not null)
        {
            try { Window = _application.GetAllTopLevelWindows(_automation).FirstOrDefault(); }
            catch { /* Preserve the original startup failure even when UIA is unavailable. */ }
        }
        try { FailureDiagnostics.Save(Data, Window, Finder.TargetId, exception); }
        catch (Exception diagnosticError) { _output.Enqueue($"诊断采集失败（保留原异常）：{diagnosticError}"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception e) { RecordFailure(e); }
        }
        try { Close(); }
        catch (Exception e)
        {
            RecordFailure(e);
            Attempt(() =>
            {
                if (_process is not { HasExited: false }) return;
                _process.Kill(entireProcessTree: true);
                if (!_process.WaitForExit(5000)) _cleanup = "清理超时，仍有自有进程";
                else _cleanup = "关闭超时后已清理自有进程树";
            });
        }
        string? after = null;
        Attempt(() => after = Data.Snapshot());
        if (after is not null && Data.Before != after)
            RecordFailure(new InvalidOperationException("正式数据目录的文件元数据发生变化；隔离验收失败。"));
        if (_failure is null) Attempt(Data.CleanTemporaryFiles);
        Attempt(() => File.WriteAllLines(Path.Combine(Data.Directory, "application.log"), _output.Select(FailureDiagnostics.Redact)));
        Attempt(() => _application?.Dispose());
        Attempt(() => _automation?.Dispose());
        Attempt(() => _process?.Dispose());
        Attempt(() => Data.WriteJson("report", new
        {
            TestId,
            Category,
            Result = _failure is null ? "Passed" : "Failed",
            DurationMs = _watch.Elapsed.TotalMilliseconds,
            Steps = _steps,
            Cleanup = _cleanup,
            ProtectedDataUnchanged = Data.Before == after,
            Error = _failure is null ? null : FailureDiagnostics.Redact(_failure.ToString()),
            TargetId = Finder.TargetId
        }));
        if (_failure is not null) throw new InvalidOperationException($"{TestId} 失败，报告：{Data.Directory}", _failure);
    }
}
