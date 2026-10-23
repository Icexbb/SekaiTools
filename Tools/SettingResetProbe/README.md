# 设置项还原验证

在 Windows 上运行：

```powershell
dotnet run --project Tools/SettingResetProbe/SettingResetProbe.csproj -- Tools/SettingResetProbe/output
```

探针在指定输出目录创建独立 `settings.json`（重复运行会清除该探针文件），不使用用户的设置文件。创建离屏 WPF 窗口，检查 20 个设置卡片及分组入口的默认隐藏、修改显示、点击还原、其他设置隔离和持久化，并生成 `settings.png` 供检查图标及标题布局。

周期彩色选项不可用时跳过其启用操作，保留默认状态检查。默认值来自 `Model.Setting.Default`；打字机设置的两个参数独立限制为非负，避免原有相互限值阻止恢复默认值（字符时间 80，渐变时间 50）。
