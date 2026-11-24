# Fluent Workbench 自动化契约

所有页面从当前测试进程的 `MainWindow.Root` 开始定位。页面切换、对话框关闭和列表回收后重新查询元素。表中列出测试依赖的主要契约，具体动作与断言由对应 FlaUI 用例维护。

| 页面或范围 | AutomationId | 控件和交互 | 用例 |
| --- | --- | --- | --- |
| 外壳 | `MainWindow.Navigation.{Subtitle,Translation,StoryDownload,Encoding,Settings}` | 导航项按模块键生成，聚焦后按 Enter。用 ItemStatus 检查当前页面 | UI-SMOKE-001、UI-DESIGN-002、UI-DISPLAY-001 |
| 外壳 | `MainWindow.ToggleNavigation`、`MainWindow.OpenLogs`、`RuntimeLog.Root` | 按钮 Invoke，日志窗口按所属进程与窗口 ID 定位 | UI-DESIGN-005 |
| 翻译 | `Translation.Root`、`Translation.LoadScriptButton`、`Translation.SearchBox`、`Translation.OpenEditorOptions` | 页面容器，按钮 Invoke，搜索 Edit Value，编辑选项浮层按内部控件 ID 定位 | UI-TRANSLATION-001/002/004 |
| 翻译行 | `Translation.Dialog.{n}.TargetText`、`Translation.Effect.{n}.TargetText`、`Translation.Row.{n}.Actions` | 当前已载入剧本内的原始事件序号，筛选和虚拟化后保留。定位范围是当前剧本；加载另一剧本时重新建立行引用。编辑使用 Value，行菜单使用按钮入口 | UI-TRANSLATION-002/003/006/011 |
| 字幕 | `Subtitle.Root`、`Subtitle.AddTaskButton`、`Subtitle.AddTaskDialog` | 页面容器、按钮 Invoke、弹窗内定位文件输入和确认按钮 | UI-SUBTITLE-001/002 |
| 字幕任务 | `Subtitle.ExportButton` 及任务卡内操作 | 按当前任务范围定位，导出操作通过按钮调用。队列等待项的拖动柄用鼠标操作 | UI-SUBTITLE-002/003/005/006 |
| 压制 | `Encoding.Root`、`Encoding.AddTaskButton`、`Encoding.AddTaskDialog`、`Encoding.CustomCrf` | 页面容器、按钮 Invoke、弹窗定位、CRF RangeValue | UI-ENCODING-001/002 |
| 压制任务 | `Encoding.AdvancedDetails`、`Encoding.TaskLog`、`Encoding.FollowLog`、`Encoding.Queue.ReorderGrip` | 展开使用 ExpandCollapse，日志 Value，跟随开关 Toggle。拖动柄在当前队列中按任务容器限定 | UI-ENCODING-001/003/005 |
| 下载 | `StoryDownload.Root`、`StoryDownload.Source`、`StoryDownload.RefreshButton` | 页面容器、来源选择和刷新按钮 | UI-DOWNLOAD-001/002 |
| 下载条目与任务 | `StoryDownload.Choice.{category}.{hash}`、`StoryDownload.Task.{guid}`、`StoryDownload.Card.{id}.PreviewArtwork` | 条目键按类别和条目键生成，任务 GUID 在任务实例内固定，卡图使用卡片 ID。重复的重试等子按钮在任务容器内定位 | UI-DOWNLOAD-001/002 |
| 设置 | `Settings.Root`、`Settings.Field.{Property}`、`Settings.ResetAll` | 属性键生成编辑器 ID，按控件使用 SelectionItem、Value 或 Toggle。还原按钮使用 Invoke | UI-SETTINGS-001/003/004 |
| 设置与关于 | `Settings.About`、`Settings.RuntimeLog`、`Settings.DeveloperGroup`、`Settings.Threshold.{Property}` | 展开使用 ExpandCollapse，日志入口 Invoke，阈值使用 Value。令牌输入仅暴露掩码 | UI-SETTINGS-002/003 |

完整设置控件契约见 [M6 验收](fluent-workbench-m6-acceptance.md)。启动、固定样本、资源校验和报告路径见 [UI 自动化运行手册](ui-automation.md)。
