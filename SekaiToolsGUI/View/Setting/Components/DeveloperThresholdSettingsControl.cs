using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SekaiToolsCore.Process.Config;
using SekaiToolsGUI.Service;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SekaiToolsGUI.View.Setting.Components;

/// <summary>Edits the same validated, persisted threshold snapshot as the HTTP API.</summary>
public sealed class DeveloperThresholdSettingsControl : StackPanel
{
    private readonly Dictionary<string, NumberBox> _editors = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly DeveloperModeService _service;

    public DeveloperThresholdSettingsControl() : this(DeveloperModeService.Instance) { }

    internal DeveloperThresholdSettingsControl(DeveloperModeService service)
    {
        _service = service;
        Children.Add(new TextBlock
        {
            Text = "识别阈值：保存后用于之后启动的识别任务，正在运行的任务不受影响。与 HTTP API 共用同一份参数。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var labels = new Dictionary<string, string>
        {
            [nameof(MatchingThreshold.DialogNametagNormal)] = "普通对话 · 名牌",
            [nameof(MatchingThreshold.DialogNametagSpecial)] = "特殊对话 · 名牌",
            [nameof(MatchingThreshold.DialogContentNormal)] = "普通对话 · 正文",
            [nameof(MatchingThreshold.DialogContentSpecial)] = "特殊对话 · 正文",
            [nameof(MatchingThreshold.BannerNormal)] = "横幅",
            [nameof(MatchingThreshold.MarkerNormal)] = "标记"
        };
        foreach (var (name, label) in labels)
        {
            var number = new NumberBox
            {
                Minimum = 0,
                Maximum = 1,
                SmallChange = 0.01,
                LargeChange = 0.1,
                Width = 150,
                ClearButtonEnabled = false
            };
            AutomationProperties.SetName(number, $"{label}识别阈值");
            number.ToolTip = name;
            _editors.Add(name, number);
            Children.Add(new CardControl
            {
                Header = label,
                Icon = new SymbolIcon(SymbolRegular.Beaker24),
                Content = number,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var save = new Button { Content = "保存阈值", Icon = new SymbolIcon(SymbolRegular.Save24) };
        var reset = new Button
        {
            Content = "恢复默认来源",
            Icon = new SymbolIcon(SymbolRegular.ArrowUndo24),
            Margin = new Thickness(8, 0, 0, 0)
        };
        save.Click += (_, _) => Execute(() =>
        {
            var patch = JsonSerializer.SerializeToElement(_editors.ToDictionary(pair => pair.Key, pair => pair.Value.Value));
            _service.UpdateThresholds(patch);
            _status.Text = "阈值已保存，将用于之后启动的识别任务。";
        });
        reset.Click += (_, _) => Execute(() =>
        {
            _service.ResetThresholds();
            _status.Text = "已恢复资源文件或内置默认阈值。";
        });
        actions.Children.Add(save);
        actions.Children.Add(reset);
        Children.Add(actions);
        Children.Add(_status);
        Loaded += (_, _) =>
        {
            _service.ThresholdsChanged += Reload;
            Reload();
        };
        Unloaded += (_, _) => _service.ThresholdsChanged -= Reload;
        IsVisibleChanged += (_, _) => { if (IsVisible) Reload(); };
    }

    private void Execute(Action action)
    {
        try { action(); }
        catch (Exception ex) { _status.Text = $"操作失败：{ex.Message}"; }
    }

    private void Reload()
    {
        Execute(() =>
        {
            var values = _service.EffectiveThresholds();
            foreach (var (name, editor) in _editors)
                editor.Value = (double)typeof(MatchingThreshold).GetProperty(name)!.GetValue(values)!;
            _status.Text = _service.Thresholds.HasValue ? "当前使用开发者阈值覆盖。" : "当前使用资源文件或内置默认阈值。";
        });
    }
}
