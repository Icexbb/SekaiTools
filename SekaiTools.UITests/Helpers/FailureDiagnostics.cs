using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;

namespace SekaiTools.UITests.Helpers;

public static class FailureDiagnostics
{
    public static void Save(TestDataManager data, Window? window, string? targetId, Exception exception,
        string folder = "Failures")
    {
        var directory = Path.Combine(data.Directory, folder);
        Directory.CreateDirectory(directory);
        var errors = new List<string>();
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception e) { errors.Add(e.ToString()); }
        }

        string? title = null;
        string? bounds = null;
        object? target = null;
        object? parent = null;
        Attempt(() => title = window?.Title);
        Attempt(() => bounds = window?.BoundingRectangle.ToString());
        if (window is not null)
        {
            Attempt(() =>
            {
                var element = targetId is null ? null : window.FindFirstDescendant(cf => cf.ByAutomationId(targetId));
                if (element is null) return;
                target = Describe(element);
                if (element.Parent is { } container) parent = Describe(container);
            });
            Attempt(() => ScreenshotHelper.Capture(window, directory, "window"));
            Attempt(() => File.WriteAllText(Path.Combine(directory, "uia-tree.txt"), DumpTree(window)));
        }
        data.WriteJson(folder + "-diagnostic", new { TargetId = targetId, Target = target, Parent = parent, WindowTitle = title, Bounds = bounds,
            Exception = Redact(exception.ToString()), DiagnosticErrors = errors.Select(Redact),
            Screenshot = Path.Combine(directory, "window.png"), Tree = Path.Combine(directory, "uia-tree.txt") });
    }

    private static object Describe(AutomationElement element) => new
    {
        Id = element.Properties.AutomationId.ValueOrDefault,
        Name = Redact(element.Properties.Name.ValueOrDefault ?? ""),
        Type = element.ControlType.ToString(), Bounds = element.BoundingRectangle.ToString(),
        Invoke = element.Patterns.Invoke.IsSupported, Value = element.Patterns.Value.IsSupported,
        SelectionItem = element.Patterns.SelectionItem.IsSupported
    };

    private static string DumpTree(AutomationElement root)
    {
        var text = new StringBuilder();
        var watch = Stopwatch.StartNew();
        var count = 0;
        void Visit(AutomationElement element, int depth)
        {
            if (++count > 600 || depth > 14 || watch.Elapsed > TimeSpan.FromSeconds(10)) return;
            try
            {
                text.Append(' ', depth * 2).Append(element.ControlType).Append(" Id=").Append(element.AutomationId)
                    .Append(" Name=").Append(Redact(element.Name)).Append(" Enabled=").Append(element.IsEnabled)
                    .Append(" Bounds=").Append(element.BoundingRectangle).AppendLine();
                foreach (var child in element.FindAllChildren()) Visit(child, depth + 1);
            }
            catch (Exception e) { text.AppendLine($"[unavailable: {e.Message}]"); }
        }
        Visit(root, 0);
        return text.ToString();
    }

    public static string Redact(string value) => Regex.Replace(value, @"Bearer\s+[^\s""\\]+", "Bearer [redacted]");
}
