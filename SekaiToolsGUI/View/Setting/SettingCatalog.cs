using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SekaiToolsGUI.ViewModel.Setting;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Setting;

public sealed class SettingSection
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Icon { get; init; } = "Settings24";
    public string? Custom { get; init; }
    public string? StatusProperty { get; init; }
    public int Columns { get; init; }
    public bool IsExpanded { get; init; }
    public SettingField[] Fields { get; init; } = [];
    public SettingSection[] Children { get; init; } = [];
    public IEnumerable<string> Properties => Fields.Select(editor => editor.Property)
        .Concat(Children.SelectMany(child => child.Properties)).Distinct();
}

public sealed class SettingField
{
    public string Property { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Label { get; init; } = "";
    public string[] Options { get; init; } = [];
    public double Width { get; init; } = double.NaN;
    public double Minimum { get; init; } = 0;
    public double Maximum { get; init; } = int.MaxValue;
    public double SmallChange { get; init; } = 1;
    public double LargeChange { get; init; } = 10;
    public string? VisibilityProperty { get; init; }
    public string? EnabledProperty { get; init; }
}

public static class SettingCatalog
{
    public static SettingSection[] Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Configuration", "settings-ui.json");
        if (File.Exists(path))
        {
            try { return Parse(File.ReadAllText(path)); }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                SekaiToolsBase.Logger.Log($"设置界面配置加载失败，使用内置配置：{exception.Message}", Microsoft.Extensions.Logging.LogLevel.Warning);
            }
        }
        using var source = typeof(SettingCatalog).Assembly.GetManifestResourceStream("SekaiToolsGUI.settings-ui.json")!;
        return Parse(new StreamReader(source).ReadToEnd());
    }

    public static SettingSection[] Parse(string json)
    {
        var sections = JsonSerializer.Deserialize<SettingSection[]>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("设置界面配置为空");
        var properties = new HashSet<string>();
        var customs = new HashSet<string>();
        foreach (var section in sections) Validate(section, properties, customs);
        return sections;
    }

    private static void Validate(SettingSection section, HashSet<string> properties, HashSet<string> customs)
    {
        if (section == null || section.Fields == null || section.Children == null)
            throw new InvalidDataException("设置卡片及其字段列表不能为 null");
        if (section.Custom is { } custom)
        {
            if (custom is not ("logs" or "about" or "subtitle-export") || !customs.Add(custom) || section.Fields.Length > 0 || section.Children.Length > 0)
                throw new InvalidDataException($"无效的自定义设置区域：{custom}");
            return;
        }
        if (string.IsNullOrWhiteSpace(section.Title) || !Enum.TryParse<SymbolRegular>(section.Icon, out _)
            || section.Columns < 0 || (section.Fields.Length == 0) == (section.Children.Length == 0))
            throw new InvalidDataException($"设置卡片配置错误：{section.Title}");
        CheckBinding(section.StatusProperty, typeof(string));
        foreach (var field in section.Fields)
        {
            if (field == null || field.Options == null) throw new InvalidDataException("设置字段及其选项不能为 null");
            var expected = field.Kind switch
            {
                "choice" or "number" => typeof(int),
                "text" or "font" or "directory" => typeof(string),
                "toggle" or "check" => typeof(bool),
                _ => throw new InvalidDataException($"未知设置控件类型：{field.Kind}")
            };
            CheckBinding(field.Property, expected, true);
            if (typeof(Model.Setting).GetProperty(field.Property)?.PropertyType != expected || !properties.Add(field.Property))
                throw new InvalidDataException($"设置项缺少默认值或重复：{field.Property}");
            CheckBinding(field.VisibilityProperty, typeof(System.Windows.Visibility));
            CheckBinding(field.EnabledProperty, typeof(bool));
            if (field.Kind == "choice" && field.Options.Length == 0 || field.Minimum > field.Maximum
                || !double.IsNaN(field.Width) && (!double.IsFinite(field.Width) || field.Width <= 0))
                throw new InvalidDataException($"设置控件参数错误：{field.Property}");
            if (expected == typeof(int))
            {
                var defaultValue = (int)typeof(Model.Setting).GetProperty(field.Property)!.GetValue(Model.Setting.Default)!;
                if (field.Kind == "choice" && (defaultValue < 0 || defaultValue >= field.Options.Length)
                    || field.Kind == "number" && (defaultValue < field.Minimum || defaultValue > field.Maximum))
                    throw new InvalidDataException($"设置控件范围未包含默认值：{field.Property}");
            }
        }
        foreach (var child in section.Children) Validate(child, properties, customs);
    }

    private static void CheckBinding(string? name, Type type, bool writable = false)
    {
        if (name == null) return;
        var property = typeof(SettingPageModel).GetProperty(name);
        if (property?.PropertyType != type || writable && !property.CanWrite)
            throw new InvalidDataException($"设置绑定属性错误：{name}");
    }
}
