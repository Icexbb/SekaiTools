# 设置项还原验证

在 Windows 上运行：

```powershell
dotnet run --project Tools/SettingResetProbe/SettingResetProbe.csproj -- Tools/SettingResetProbe/output
```

探针在指定输出目录创建独立 `settings.json`（重复运行会清除该探针文件），不使用用户的设置文件。创建离屏 WPF 窗口，检查 21 个设置卡片及分组入口的默认隐藏、修改显示、点击还原、其他设置隔离和持久化，并生成 `settings.png` 供检查图标及标题布局。

还会用另一份配置生成不同标题、选项、数值范围及宽度的卡片，验证双向绑定、分组还原范围推导和非法配置拒绝；检查还原控件不包含 Border、使用 ArrowUndo24、左侧间距为 8px，并在明暗主题切换时跟随主题色资源。普通设置界面的配置说明见 [settings-ui-configuration.md](../../Docs/settings-ui-configuration.md)。

下载目录验证覆盖旧设置缺少字段时使用默认目录、中文和空格路径持久化、空值回退、相对路径拒绝及逐项还原。

独立日志窗口验证覆盖窗口复用、后台线程追加、等级筛选、关闭后继续收集、重新打开、清空，以及随所属窗口关闭。

周期彩色选项不可用时跳过其启用操作，保留默认状态检查。默认值来自 `Model.Setting.Default`；打字机设置的两个参数独立限制为非负，避免原有相互限值阻止恢复默认值（字符时间 80，渐变时间 50）。
