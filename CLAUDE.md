# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此仓库中工作时提供指导。

## 构建与运行

```bash
# 构建整个解决方案
dotnet build SekaiTools.sln

# 构建单个项目
dotnet build SekaiToolsCore/SekaiToolsCore.csproj

# 发布主 GUI 程序（Release 模式，包含 Updater 打包）
dotnet publish SekaiToolsGUI/SekaiToolsGUI.csproj -c Release -o Build/

# 或使用批处理脚本（自动设置版本号并发布）
./build.bat

# 运行测试（解决方案中暂无测试项目）
dotnet test SekaiTools.sln
```

基于 .NET 8 的 Visual Studio 解决方案，目标平台为 Windows。所有项目通过 `Directory.Build.props` 全局启用可空引用类型。

## 架构概览

### 解决方案结构

| 项目 | 目标框架 | 职责 |
|---------|--------|------|
| **SekaiToolsBase** | net8.0 | 共享数据模型：游戏剧本解析、ASS 字幕格式、代理/日志 |
| **SekaiToolsCore** | net8.0-windows | 视频处理引擎：OpenCV 模板匹配 → 帧集合 → ASS 字幕生成 |
| **SekaiToolsGUI** | net8.0-windows (WPF) | 主桌面应用，使用 WPF-UI，MVVM 导航 |
| **SekaiDataFetch** | net8.0 | 从远程 API 下载并缓存游戏剧本数据 |
| **Updater** | net8.0-windows (WPF) | 独立更新程序，随 GUI 发布输出一同打包 |
| **SekaiToolsMauiText** | net10.0-多平台 | 跨平台 MAUI 移植版（独立代码库，仅引用 SekaiToolsBase） |

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

### MVVM 模式（GUI）

`MainWindow`（FluentWindow）使用 WPF-UI 的 `NavigationView`。`MainWindowViewModel` 定义导航项，映射到各页面类型。每个页面位于 `View/<页面名>/`，对应的 ViewModel 位于 `ViewModel/<页面名>/`。

页面实现 `IAppPage<object>` 接口，通过 `OnNavigatedTo()` 进行初始化。自定义的 `ViewModelBase` 将属性值存储在 `Dictionary<string, object>` 中，而非单独的字段。

### 程序集加载

`SekaiToolsGUI` 在 `App.xaml.cs` 中使用自定义 `AssemblyResolve` 处理程序，从 `libs/` 子目录加载 DLL。发布目标会自动将非核心 DLL 移入此目录，并删除 x86/win-arm64/browser 等多余运行时。

### 模板资源管理

`SekaiToolsCore.ResourceManager` 从 `resource.g.xbb.moe` 下载外部模板图像资源到 `~/SekaiTools/Resource/`。根据 JSON 清单校验 MD5 和文件大小。

## 调试环境变量

`VideoProcessor` 读取以下环境变量（仅在 `Debugger.IsAttached` 时生效）：
- `DebugFrameID` — 从指定帧开始处理
- `DebugTargetString` / `DebugTargetSpeaker` — 匹配到指定文本/角色时提前停止 DialogMatcher
- `DebugEarlyTermination` — 提前终止后继续处理的额外帧数
- `DebugIgnoreBannerMarker` — 完全跳过横幅/标记匹配
- `DebugShowImg` — 通过 `CvInvoke.Imshow` 显示中间模板匹配图像
- `DebugImgWait` — 每张调试图像等待按键后继续
