# 设置页界面配置

普通设置卡片由 [settings-ui.json](../SekaiToolsGUI/Configuration/settings-ui.json) 定义，由通用 `SettingViewBuilder` 生成界面，无需逐项编写 XAML。

## 修改入口

- 开发时修改 `SekaiToolsGUI/Configuration/settings-ui.json`，构建时复制到输出及发布目录的 `Configuration/settings-ui.json`。
- 已发布程序可直接修改程序目录下的 `Configuration/settings-ui.json`，重启程序后读取；不需要重新编译或修改 XAML。
- 外部配置不存在或无法读取、解析、校验时，使用程序集内置配置；失败原因写入运行日志。
- 用户选择的设置值仍保存在原来的 `Data/setting.json`，两种配置用途不同。

## 配置结构

顶层是有序数组，数组顺序就是页面顺序。每个普通项声明 `fields`（编辑控件）或 `children`（分组子项），二者择一。示例：

```json
[
  {
    "title": "打字机设置",
    "description": "设置打字机的渐变时间参数",
    "icon": "TextContinuous24",
    "fields": [
      { "kind": "number", "property": "TypewriterCharTime", "minimum": 0, "width": 120 },
      { "kind": "number", "property": "TypewriterFadeTime", "minimum": 0, "width": 120 }
    ]
  }
]
```

| 卡片字段 | 用途 |
| --- | --- |
| `title`、`description` | 标题和说明，说明可省略，支持换行。 |
| `icon` | 当前 WPF-UI 支持的 `SymbolRegular` 图标名。 |
| `fields` | 按排列顺序生成的编辑控件。 |
| `children` | 嵌套卡片，自动生成可展开分组。 |
| `isExpanded` | 分组初始展开状态，默认 `false`；实验性功能保留默认折叠。 |
| `columns` | 编辑控件每行列数，省略时全部放在一行。 |
| `statusProperty` | 额外状态说明所绑定的只读字符串属性，例如实验运行库不可用原因。 |

## 编辑控件

| `kind` | 控件 | 绑定属性类型 | 特有配置 |
| --- | --- | --- | --- |
| `choice` | 下拉选择 | `int` | `options` 为按索引排列的选项文本；保留索引原有含义。 |
| `number` | 数值框 | `int` | `minimum`、`maximum`、`smallChange`、`largeChange`。范围须包含默认值。 |
| `text` | 单行输入框 | `string` | 即时更新并自动保存。 |
| `font` | 字体输入框和选择按钮 | `string` | 复用字体选择对话框。 |
| `directory` | 目录输入框和选择按钮 | `string` | 使用系统文件夹选择器。 |
| `toggle` | 带标签的开关 | `bool` | `label` 为开关左侧标签。 |
| `check` | 复选框 | `bool` | `label` 为复选框内容。 |

所有编辑控件都声明 `property`，对应 `SettingPageModel` 的可写属性。通用可选字段为 `width`、`visibilityProperty`（绑定 `Visibility` 属性）和 `enabledProperty`（绑定布尔属性）。代理输入框显隐、实验功能禁用都通过这些字段配置。

## 还原按钮与默认值

卡片根据 `fields` 自动确定还原范围，分组递归汇总所有子项，不再单独维护还原字段列表。与 `Model.Setting.Default` 比较，有任何字段偏离默认值才显示图标。点击后仅还原对应范围并自动保存。

还原入口为无外框的 `ArrowUndo24` 图标，使用动态主题色 `AccentTextFillColorPrimaryBrush`，距标题左侧 8px；支持键盘操作及无障碍名称。

调整现有属性的标题、顺序、分组、说明、图标、控件类型及参数，只需修改 JSON。新增需要保存的业务设置仍需在 `Model.Setting` 和 `SettingPageModel` 定义属性、默认值及业务用途，再加入 JSON；界面不需要新增 XAML。

## 专用内容与校验

`{ "custom": "logs" }` 和 `{ "custom": "about" }` 分别插入现有运行日志、关于页。其内部是专用功能视图，保留 XAML；在数组中移动或移除对应配置即可改变页面布局。

加载时校验字段名、图标、控件类型、属性类型、默认值范围、重复属性及重复专用区域。校验不通过的外部配置不会部分应用。

## 下载文件存储目录

`DownloadDirectory` 配置下载剧本的保存位置，默认仍为用户目录下的 `SekaiTools/Scripts`。支持直接输入绝对路径或点击“选择”使用系统文件夹选择器，自动保存并支持逐项还原。旧设置文件缺少该字段或目录留空时使用默认目录。

每次开始下载时读取并固定本批次目录，已经加入队列但尚未下载的任务也会使用新目录；下载过程中修改设置从下一批次生效。目录不存在时下载会创建目录，不迁移已下载文件。下载页面“打开下载文件夹”按钮使用当前配置目录，列表缓存仍保存在原位置。

验证命令见 [SettingResetProbe](../Tools/SettingResetProbe/README.md)。探针使用独立设置文件，覆盖配置改变界面、双向绑定、还原行为及图标样式。
