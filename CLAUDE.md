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

# 运行全部测试（当前包含 SekaiTools.Tests）
dotnet test SekaiTools.sln
```

基于 .NET 10 的解决方案。所有项目通过 `Directory.Build.props` 全局启用可空引用类型。

## Git 提交规范

Git 提交信息必须遵循 Conventional Commits（语义化提交）格式：

```text
<type>(<scope>): <中文描述>
```

- `type` 使用小写英文，常用类别包括：`feat`、`fix`、`docs`、`refactor`、`perf`、`test`、`build`、`ci`、`chore`、`revert`。
- `scope` 可选，使用简短的小写英文标识影响范围，例如 `ui`、`video`、`subtitle`、`download`、`settings`。
- 冒号后的提交描述必须使用中文，简洁说明本次提交完成的变更。
- 存在破坏性变更时，在类型或作用域后添加 `!`，并在提交正文或页脚中说明影响。

示例：

```text
feat(ui): 增加历史记录筛选功能
fix(video): 修复取消压制后进度状态未重置的问题
docs(git): 补充提交信息规范
refactor(subtitle)!: 调整字幕生成配置结构
```

## 版本发布规范

- GUI 版本号统一维护在 `SekaiToolsGUI/AssemblyInfo.cs` 的 `AssemblyVersion` 和 `AssemblyFileVersion`，两者必须保持一致。
- 版本号格式为 `主版本.次版本.补丁版本.MMdd`。前三段按语义化版本递增，最后一段使用发布日的两位月份和日期。
- 提升版本前，以最近版本标签到 `HEAD` 的提交范围为依据汇总主要变化，不遗漏用户可感知的新增、优化和修复。
- 每个版本必须新增 `release-notes/<版本号>.md`，沿用已有文档的“主要更新”结构，并按实际内容使用“新增、优化、修复、工程、测试”等分类。
- 发布说明面向用户和维护者概括重要变化，不机械复制提交信息，也不记录无关实现细节。
- 版本号与对应发布说明应纳入发布提交，提交信息使用 `chore(release): 提升版本至 <版本号>`。
- 创建标签前必须完成 Release 构建和完整测试；标签名称与版本号完全一致，并指向包含版本号和发布说明的发布提交。
- 不允许只提升版本或只创建标签而缺少发布说明。已推送标签如需改写，必须先获得用户明确确认。

## 架构概览

### 解决方案结构

| 项目 | 目标框架 | 职责 |
|---------|--------|------|
| **SekaiToolsConfiguration** | net10.0 | 统一加载、校验并随发布产物复制网络端点配置 |
| **SekaiToolsBase** | net10.0 | 游戏数据 DTO、剧本解析、翻译合并、共享日志/代理 |
| **SekaiToolsSubtitles** | net10.0 | 独立 ASS 文档、样式、标签与绘图模型 |
| **SekaiToolsCore** | net10.0 | 视频处理引擎与基础设施抽象：模板匹配 → FrameSet → 字幕生成 |
| **SekaiToolsMedia** | net10.0 | VSPipe/FFmpeg 压制管线、编码参数与进度模型 |
| **SekaiDataFetch** | net10.0 | 从远程 API 下载并缓存游戏剧本数据 |
| **SekaiToolsInfrastructure** | net10.0 | 模板资源下载校验、进度与历史文件存储 |
| **SekaiToolsGUI** | net10.0-windows (WPF) | WPF 组合根、导航、ViewModel 与 Windows 集成 |
| **Updater** | net10.0-windows (WPF) | 独立更新程序，由 GUI 发布目标构建和复制 |
| **SekaiTools.Tests** | net10.0 | 非 UI 项目的单元与兼容性测试 |

完整依赖方向和新代码放置规则见 [ARCHITECTURE.md](ARCHITECTURE.md)。

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

- **`SekaiToolsInfrastructure.Persistence.ProgressStore`** — 进度持久化。`ProcessingState` DTO 捕获全部匹配器状态和帧位置，序列化为 JSON 存入 `~/SekaiTools/Progress/{hash}.json`。应用启动时 `OnNavigatedTo` 扫描进度文件，若对应文件仍存在则弹窗询问恢复。

- **`SekaiToolsInfrastructure.Persistence.HistoryStore`** — 历史记录（最多 100 条）。处理完成后保存完整 `ProcessingState` 到独立文件 `~/SekaiTools/History/{timestamp}_{hash}.json`，同 hash 自动去重保留最新。用户可通过 `HistoryDialog` (ContentDialog) 选择加载历史记录直接导出字幕。

### 进度保存与恢复

每个匹配器 (`DialogTemplateMatcher`, `BannerTemplateMatcher`, `MarkerTemplateMatcher`) 均暴露 `SaveState()` / `RestoreState(Dto)` 方法，序列化内部状态（`_status`、回退阈值、FrameSet 数据）。`VideoProcessor.CaptureState()` 收集全部状态，`ApplyState()` 恢复状态并 seek 视频到断点。保存触发时机：每 300 帧 + 每次 FrameSet 完成时。正常完成后保留进度（仅输出字幕时清除）。

## WPF GUI 架构（SekaiToolsGUI）

### MVVM 模式

`MainWindow`（FluentWindow）使用 WPF-UI 的 `NavigationView`。`MainWindowViewModel` 定义导航项，映射到各页面类型。每个页面位于 `View/<页面名>/`，对应的 ViewModel 位于 `ViewModel/<页面名>/`。

页面实现 `IAppPage<object>` 接口，通过 `OnNavigatedTo()` 进行初始化。自定义的 `ViewModelBase` 将属性值存储在 `Dictionary<string, object>` 中，而非单独的字段。

### 程序集加载与发布整理

发布时，MSBuild 目标 `OrganizeOutput` 将非核心 DLL（除 `SekaiToolsGUI.dll` 和 `Updater.exe` 外）移至 `libs/` 子目录，并删除 x86/win-arm64/browser 等多余运行时及所有 PDB 文件。

`App.xaml.cs` 中的 `AssemblyResolve` 处理器从 `libs/` 加载被移走的程序集。

`BuildUpdater` 目标将 Updater 以 PublishSingleFile 发布为单个 `Updater.exe`，与 `7zr.exe` 一同复制到 Build 根目录。

### 模板资源管理

`SekaiToolsInfrastructure.Resources.ResourceManager` 从 `network-endpoints.json` 配置的资源服务下载外部模板图像资源到 `~/SekaiTools/Resource/`。根据 JSON 清单校验 MD5 和文件大小。

### 网络端点配置

所有运行时网络服务地址和默认数据源集中在根目录 `network-endpoints.json`。该文件由 `SekaiToolsConfiguration.NetworkEndpoints` 加载并校验，构建和发布时会复制到输出目录；若外部文件缺失，则回退到程序集内嵌的默认配置。

新增或修改远程端点时只编辑该 JSON，不在 C# 或 XAML 中写入完整 HTTPS 地址。代理协议和 XAML 命名空间不属于远程服务端点。

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
