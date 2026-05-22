# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指导。

## 构建与运行

```bash
# 构建整个解决方案
dotnet build SekaiTools.sln

# 构建单个项目
dotnet build SekaiToolsCore/SekaiToolsCore.csproj

# 发布 WPF GUI（Release 模式，包含 Updater 打包）
dotnet publish SekaiToolsGUI/SekaiToolsGUI.csproj -c Release -o Build/
# 或使用批处理脚本
./build.bat

# 发布 Avalonia 跨平台应用
dotnet publish SekaiToolsAvalonia/SekaiToolsAvalonia.csproj -c Release -o Build/Avalonia/

# 运行测试（解决方案中暂无测试项目）
dotnet test SekaiTools.sln
```

基于 .NET 10 的解决方案。所有项目通过 `Directory.Build.props` 全局启用可空引用类型。

## 架构概览

### 解决方案结构

| 项目 | 目标框架 | 职责 |
|---------|--------|------|
| **SekaiToolsBase** | net10.0 | 共享数据模型：游戏剧本解析、ASS 字幕格式、代理/日志 |
| **SekaiToolsCore** | net10.0 | 视频处理引擎：OpenCV 模板匹配 → 帧集合 → ASS 字幕生成 |
| **SekaiToolsGUI** | net10.0-windows (WPF) | WPF 桌面应用，使用 WPF-UI 4.3.0，MVVM 导航（仅 Windows） |
| **SekaiToolsAvalonia** | net10.0 (Avalonia) | Avalonia 跨平台桌面应用，支持 Windows/macOS/Linux |
| **SekaiDataFetch** | net10.0 | 从远程 API 下载并缓存游戏剧本数据 |
| **Updater** | net10.0-windows (WPF) | 独立更新程序，PublishSingleFile 打包为单个 exe |

### 核心数据流（"自动轴机"管线）

```
1. 游戏 JSON (SekaiToolsBase.GameScript.GameScript)
   + 翻译文本 (SekaiToolsBase.Story.Translation.TranslationData)
   → Story 对象（合并后的事件：对话、横幅、标记）

2. Story + 视频文件
   → VideoProcessor（通过 Emgu.CV 逐帧读取）
   → TemplateMatcherCreator 创建 4 个匹配器：
     ContentMatcher  — 先找到视频的"内容区域"
     DialogMatcher — 匹配对话名字模板
     BannerMatcher — 匹配横幅（标题卡）模板
     MarkerMatcher — 匹配标记（歌曲/MV）模板

3. 匹配到的位置 → FrameSet（DialogBaseFrameSet、BannerBaseFrameSet、MarkerBaseFrameSet）
   每个 FrameSet 包含逐帧的 (x,y) 位置和时间数据

4. FrameSet + VideoInfo + Config
   → SubtitleMaker
   → ASS 字幕文件 (SekaiToolsBase.SubStationAlpha.Subtitle)
     包含打字机效果、抖动补偿、横幅转场、遮罩效果
```

### 关键类

- **`SekaiToolsCore.VideoProcessor`** — 主处理循环。逐帧读取视频，按顺序调用模板匹配器（先内容区域，再对话→横幅→标记）。通过 `VideoProcessCallbacks` 委托报告进度。使用 `Channel<Mat>` 实现有界预览帧队列。

- **`SekaiToolsCore.SubtitleMaker`** — 将匹配到的 FrameSet 转换为带有样式的 ASS 字幕事件。处理打字机文字效果、对话抖动（逐帧位置跟踪）、横幅淡入转场和标记遮罩。

- **`SekaiToolsBase.Story.Story`** — 将解析后的游戏 JSON (`GameScript`) 与翻译数据 (`TranslationData`) 合并。翻译 `DialogStoryEvent` 条目的正文和角色名称。

- **`SekaiToolsCore.Match.TemplateMatcher.TemplateMatcher`** — 基于 OpenCV 相关性的静态模板匹配，配合 `TemplateMatchCachePool` 按帧缓存结果以避免重复计算。

- **`SekaiToolsCore.Process.ProgressStore`** — 进度持久化。`ProcessingState` DTO 捕获全部匹配器状态和帧位置，序列化为 JSON 存入 `~/SekaiTools/Progress/{hash}.json`。应用启动时 `OnNavigatedTo` 扫描进度文件，若对应文件仍存在则弹窗询问恢复。

- **`SekaiToolsCore.Process.HistoryStore`** — 历史记录（最多 100 条）。处理完成后保存完整 `ProcessingState` 到独立文件 `~/SekaiTools/History/{timestamp}_{hash}.json`，同 hash 自动去重保留最新。用户可通过 `HistoryDialog` (ContentDialog) 选择加载历史记录直接导出字幕。

### 进度保存与恢复

每个匹配器 (`DialogTemplateMatcher`, `BannerTemplateMatcher`, `MarkerTemplateMatcher`) 均暴露 `SaveState()` / `RestoreState(Dto)` 方法，序列化内部状态（`_status`、回退阈值、FrameSet 数据）。`VideoProcessor.CaptureState()` 收集全部状态，`ApplyState()` 恢复状态并 seek 视频到断点。保存触发时机：每 300 帧 + 每次 FrameSet 完成时。正常完成后保留进度（仅输出字幕时清除）。

## Avalonia 应用架构

### 入口与启动

`Program.cs` → `AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args)`

`App.axaml.cs` 在 `OnFrameworkInitializationCompleted` 中设置 `desktop.MainWindow = new MainWindow()`。

### 导航系统

`MainWindow` 使用两个 `ListBox`（NavListBox 主导航 + FooterListBox 底部导航），绑定到 `MainWindowViewModel.NavigationItems` / `NavigationFooterItems`。

`NavItem` 包含 `TargetPageType`（Type）、`Content`（显示名）、`Icon`（图标文本）、`CachePage`（是否缓存页面实例）。

页面切换通过 `Activator.CreateInstance(pageType)` 创建实例，放入 `ContentControl PageContent`。设置 `CachePage = true` 的页面会被缓存在 `_pageCache` 字典中，再次导航时直接复用并调用 `OnNavigatedTo()`。

### 页面与组件

每个功能页面位于 `View/<功能名>/`，对应的 ViewModel 位于 `ViewModel/<功能名>/`。页面实现 `IAppPage` 接口，`OnNavigatedTo()` 在每次导航到该页面时调用（用于初始化、检查资源等）。

可复用的 UI 组件位于 `View/<功能名>/Components/`，如 `DialogLine`、`BannerLine`、`MarkerLine` 等。这些组件通过 C# 代码手动实例化（非 XAML DataTemplate），因此不需要公共无参构造函数——项目通过 `<NoWarn>AVLN3001</NoWarn>` 抑制相关 Avalonia 警告。

### ViewModel 基类

`ViewModelBase` 将属性值存储在 `Dictionary<string, object>` 中（而非独立字段），通过 `GetProperty<T>(defaultValue)` / `SetProperty<T>(value)` 访问，自动触发 `PropertyChanged`。调用方使用 `[CallerMemberName]` 推断属性名。

`SubtitlePageModel` 等页面级 ViewModel 继承 `ViewModelBase`；`SettingPageModel` 使用单例模式（`.Instance`），每次属性变更自动调用 `SaveSetting()` 持久化到 `~/SekaiTools/Data/setting.json`。

### 设置持久化

`SettingPageModel`（单例） ↔ `Model.Setting`（DTO struct） ↔ `~/SekaiTools/Data/setting.json`

- `LoadSetting()` 在 `MainWindowViewModel` 构造时调用，从 JSON 反序列化恢复全部设置
- 任何属性 setter 自动触发 `SaveSetting()` 写入文件
- `Model.Setting` 通过 `System.Text.Json` 序列化，使用 `JavaScriptEncoder.Create(UnicodeRanges.All)` 确保中文字符不被转义

### Snackbar 通知

`SnackbarService` 在 `MainWindow.OnInitialized()` 中初始化，通过静态属性 `MainWindow.Snackbar` 全局访问。调用 `MainWindow.Snackbar?.Show("消息")` 显示带淡入淡出动画的底部通知条。

### 跨平台原生依赖

`Emgu.CV` 的运行时包按平台条件引用：
```xml
<PackageReference Include="Emgu.CV.runtime.windows" Condition="...IsOSPlatform('Windows')" />
<PackageReference Include="Emgu.CV.runtime.macos"   Condition="...IsOSPlatform('OSX')" />
<PackageReference Include="Emgu.CV.runtime.ubuntu"  Condition="...IsOSPlatform('Linux')" />
```

## WPF GUI 架构（SekaiToolsGUI）

### MVVM 模式

`MainWindow`（FluentWindow）使用 WPF-UI 的 `NavigationView`。`MainWindowViewModel` 定义导航项，映射到各页面类型。每个页面位于 `View/<页面名>/`，对应的 ViewModel 位于 `ViewModel/<页面名>/`。

页面实现 `IAppPage<object>` 接口，通过 `OnNavigatedTo()` 进行初始化。自定义的 `ViewModelBase` 将属性值存储在 `Dictionary<string, object>` 中，而非单独的字段。

### 程序集加载与发布整理

发布时，MSBuild 目标 `OrganizeOutput` 将非核心 DLL（除 `SekaiToolsGUI.dll` 和 `Updater.exe` 外）移至 `libs/` 子目录，并删除 x86/win-arm64/browser 等多余运行时及所有 PDB 文件。

`App.xaml.cs` 中的 `AssemblyResolve` 处理器从 `libs/` 加载被移走的程序集。

`BuildUpdater` 目标将 Updater 以 PublishSingleFile 发布为单个 `Updater.exe`，与 `7zr.exe` 一同复制到 Build 根目录。

### 模板资源管理

`SekaiToolsCore.ResourceManager` 从 `resource.g.xbb.moe` 下载外部模板图像资源到 `~/SekaiTools/Resource/`。根据 JSON 清单校验 MD5 和文件大小。

## NuGet 依赖注意事项

- `System.Text.Json` 无需显式 PackageReference — `net10.0` 共享框架已内置
- `Microsoft.Extensions.*` 系列包版本须与目标框架匹配（当前 10.0.8），不可使用 .NET 11 预览版
- `System.Drawing.Common` 版本须与目标框架匹配（当前 10.0.8），其传递依赖 `System.Private.Windows.Core` 会要求匹配版本的 `System.Reflection.Metadata`
- 已移除 `TextCopy`，改用内置剪贴板 API

## 调试环境变量

`VideoProcessor` 读取以下环境变量（仅在 `Debugger.IsAttached` 时生效）：
- `DebugFrameID` — 从指定帧开始处理
- `DebugTargetString` / `DebugTargetSpeaker` — 匹配到指定文本/角色时提前停止 DialogMatcher
- `DebugEarlyTermination` — 提前终止后继续处理的额外帧数
- `DebugIgnoreBannerMarker` — 完全跳过横幅/标记匹配
- `DebugShowImg` — 通过 `CvInvoke.Imshow` 显示中间模板匹配图像
- `DebugImgWait` — 每张调试图像等待按键后继续
