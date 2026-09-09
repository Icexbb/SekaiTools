# Popover

在页面声明 `xmlns:control="clr-namespace:SekaiToolsGUI.Control"`，通过 `Popover.Target` 内嵌目标控件，其余内容为浮层内容：

```xml
    <control:Popover HorizontalAlignment="Left" VerticalAlignment="Top"
                     IsOpen="{Binding IsChecked, ElementName=HelpButton, Mode=TwoWay}">
        <control:Popover.Target>
            <ToggleButton x:Name="HelpButton" Content="帮助" />
        </control:Popover.Target>
        <StackPanel MaxWidth="320">
            <TextBlock Text="操作提示" FontWeight="SemiBold" />
            <TextBlock Margin="0,8,0,0" Text="在此放置说明或交互控件。" TextWrapping="Wrap" />
        </StackPanel>
    </control:Popover>
```

- `Content` 支持任意内容，并支持 `ContentTemplate`、`ContentTemplateSelector`。
- `Target` 支持任意 UIElement（包括 Button），在页面布局中正常显示并继承数据上下文。它仅设定目标，打开动作通过 `IsOpen` 绑定或 `Open()` 控制。
- 默认使用 `Target` 定位；显式设置 `PlacementTarget` 可覆盖锚点，兼容原有外部目标用法。
- `IsOpen` 默认双向绑定，也可以调用 `Open()` / `Close()`，不会覆盖已有绑定。
- `Placement` 默认 `Bottom`，偏移由 `HorizontalOffset` / `VerticalOffset` 控制（默认 0 / 8）。
- `StaysOpen` 默认 `false`，点击外部关闭；设为 `true` 可禁用该行为。
- Esc、宿主窗口失焦、移动、调整大小及控件卸载时关闭浮层。
- 打开时焦点进入浮层，Tab 在浮层内循环。通过 `Opened` / `Closed` 监听实际显示状态。
- `Background`、`Foreground`、`BorderBrush` 使用应用动态主题资源；支持 `Padding`、`BorderThickness`、`CornerRadius`（默认 8）。
- 只有 `Target` 占据普通布局空间，浮层不占位。需要限制浮层尺寸时，请在内容元素上设置 `Width` / `MaxWidth` 等属性。
