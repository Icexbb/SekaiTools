using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using SekaiToolsCore.Process.Config;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsInfrastructure.Development;

namespace SekaiToolsGUI.Service;

public sealed class DeveloperModeService : IDisposable
{
    public static DeveloperModeService Instance { get; } = new();
    private DeveloperApiServer? _server;
    private readonly string _path = Path.Combine(ResourceManager.DataBaseDir, "Data", "developer-thresholds.json");
    public MatchingThreshold? Thresholds { get; private set; }

    public void Initialize()
    {
        SettingPageModel.Instance.PropertyChanged += SettingsChanged;
        UpdateServer();
    }

    private void SettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingPageModel.DeveloperMode)) UpdateServer();
    }

    private void UpdateServer()
    {
        _server?.Dispose();
        _server = null;
        Thresholds = null;
        var settings = SettingPageModel.Instance;
        settings.DeveloperApiStatus = "已关闭";
        if (!settings.DeveloperMode) return;
        try
        {
            if (File.Exists(_path))
                Thresholds = DeveloperThresholds.Patch(new MatchingThreshold(), JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(_path)));
            var server = new DeveloperApiServer(18339, (method, path, body) =>
                Application.Current.Dispatcher.InvokeAsync(() => Dispatch(method, path, body)).Task);
            _server = server;
            server.Start();
            settings.DeveloperApiStatus = $"{server.Address}\nBearer {server.Token}";
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            settings.DeveloperApiStatus = $"接口启动失败：{ex.Message}；可关闭后重新启用以重试";
        }
    }

    private MatchingThreshold EffectiveThresholds()
    {
        if (Thresholds is { } value) return value;
        var path = ResourceManager.Instance.ResourcePath(ResourceType.VideoProcess, "thresholds.json");
        return File.Exists(path)
            ? DeveloperThresholds.Patch(new MatchingThreshold(), JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path)))
            : new MatchingThreshold();
    }

    private object Dispatch(string method, string path, JsonElement body)
    {
        var settings = SettingPageModel.Instance;
        if (!settings.DeveloperMode) throw new ArgumentException("开发者模式已关闭");
        switch (method, path)
        {
            case ("GET", "/api/v1/status"):
                return new
                {
                    version = SettingPageModel.AppVersion,
                    developerMode = true,
                    processId = Environment.ProcessId,
                    thresholdOverrides = Thresholds.HasValue,
                    appliesTo = "之后启动的识别任务"
                };
            case ("GET", "/api/v1/thresholds"):
                return EffectiveThresholds();
            case ("PATCH", "/api/v1/thresholds"):
                var next = DeveloperThresholds.Patch(EffectiveThresholds(), body);
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(next));
                File.Move(temp, _path, true);
                Thresholds = next;
                return next;
            case ("DELETE", "/api/v1/thresholds"):
                File.Delete(_path);
                Thresholds = null;
                return EffectiveThresholds();
            case ("GET", "/api/v1/settings"):
                return new
                {
                    settings.RecognitionPerformanceMode,
                    settings.RecognitionMemoryBudgetMiB,
                    settings.RecognitionFrameBufferChoice,
                    settings.RecognitionPrepareGray
                };
            case ("PATCH", "/api/v1/settings"):
                PatchSettings(body, settings);
                return Dispatch("GET", path, body);
            case ("POST", "/api/v1/window/activate"):
                var window = Application.Current.MainWindow;
                if (window == null) throw new ArgumentException("主窗口尚未就绪");
                window.Show();
                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                return new { activated = window.Activate() };
            case ("POST", "/api/v1/navigation"):
                if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("page", out var page)
                    || page.ValueKind != JsonValueKind.String) throw new ArgumentException("需要字符串 page");
                var pageType = page.GetString() switch
                {
                    "subtitle" => typeof(View.Subtitle.SubtitlePage),
                    "translate" => typeof(View.Translate.TranslatePage),
                    "download" => typeof(View.Download.DownloadPage),
                    "suppress" => typeof(View.Suppress.SuppressPage),
                    "settings" => typeof(View.Setting.SettingPage),
                    _ => throw new ArgumentException("未知页面")
                };
                if (Application.Current.MainWindow is not MainWindow main) throw new ArgumentException("主窗口尚未就绪");
                main.Navigate(pageType);
                return new { page = page.GetString() };
            default:
                throw new KeyNotFoundException("未知路由或方法，请参阅 docs/developer-api.md");
        }
    }

    private static void PatchSettings(JsonElement body, SettingPageModel settings)
    {
        if (body.ValueKind != JsonValueKind.Object) throw new ArgumentException("设置必须是 JSON 对象");
        var updates = new List<Action>();
        foreach (var item in body.EnumerateObject())
        {
            if (item.Name == nameof(settings.RecognitionPrepareGray))
            {
                if (item.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException("RecognitionPrepareGray 必须是布尔值");
                var enabled = item.Value.GetBoolean();
                updates.Add(() => settings.RecognitionPrepareGray = enabled);
                continue;
            }
            var range = item.Name switch
            {
                nameof(settings.RecognitionPerformanceMode) => (0, 2),
                nameof(settings.RecognitionMemoryBudgetMiB) => (128, 4096),
                nameof(settings.RecognitionFrameBufferChoice) => (0, 3),
                _ => throw new ArgumentException($"不支持修改：{item.Name}")
            };
            if (item.Value.ValueKind != JsonValueKind.Number || !item.Value.TryGetInt32(out var value) || value < range.Item1 || value > range.Item2)
                throw new ArgumentException($"{item.Name} 必须是 {range.Item1}～{range.Item2} 的整数");
            var property = typeof(SettingPageModel).GetProperty(item.Name)!;
            updates.Add(() => property.SetValue(settings, value));
        }
        foreach (var update in updates) update();
    }

    public void Dispose()
    {
        SettingPageModel.Instance.PropertyChanged -= SettingsChanged;
        _server?.Dispose();
        _server = null;
    }
}
