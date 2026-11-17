# Fluent Workbench：Phase 0 审计与实施计划

审计日期：2026-11-17（Asia/Hong_Kong）。代码基线：`b21fece`，分支 `main`，开始审计时工作区干净。

## 1. 结论与证据边界

现有 WPF / WPF-UI / MVVM 可以承载本次改造，无需更换框架或重写业务。应先建立测试隔离、稳定 AutomationId 和主题资源层，再以翻译编辑器验证设计系统，随后推进字幕、压制、下载和设置。

当前最大的前置缺口是 UI 自动化工程基础：没有独立 FlaUI 项目，没有显式 AutomationId 契约，正式启动会访问用户配置、恢复记录和资源服务。直接对正式实例执行回归不满足数据隔离要求。

本报告完成源代码和本地依赖审计，提供 Phase 1–5 的实施方案。没有修改生产代码、启动正式应用、运行 FlaUI、采集截图或执行构建和业务测试；本文不构成视觉验收或运行通过报告。布局问题均为源码风险判断，需由后续实际截图确认。Phase 1 的方案和命令尚未实现。

## 2. 技术与工程基线

| 项目 | 已核实状态 | 依据 |
| --- | --- | --- |
| Windows GUI | `net10.0-windows10.0.19041.0`，WPF，`win-x64` | `SekaiToolsGUI/SekaiToolsGUI.csproj` |
| WPF-UI | 声明与本地 restore assets 均为 **4.3.0** | 同上；`SekaiToolsGUI/obj/project.assets.json` |
| 本机 SDK | `10.0.403` | `dotnet --version` |
| 单元测试 | xUnit `2.9.3`，runner `4.0.0`，Test SDK `18.10.1`，`net10.0` | `SekaiTools.Tests/SekaiTools.Tests.csproj` |
| UI 自动化 | 仓库检索未发现 FlaUI / UIA3 集成 | 解决方案、项目和源码检索 |
| DPI | manifest 声明 PerMonitorV2 / PerMonitor | `SekaiToolsGUI/app.manifest` |
| 发布布局 | Publish 时构建 Updater，整理 DLL 到 `libs/`，生成更新清单 | GUI csproj；与普通 Build 布局不同 |
| 架构边界 | GUI 为 Windows 组合根；Core/Media 不反向引用 GUI | `ARCHITECTURE.md` |

主解决方案已有 11 个项目，包含 `SekaiToolsHost`；macOS SwiftUI 客户端不属于本次 Windows UI 改造范围。不要依据旧项目清单遗漏 Host 的业务兼容性测试。

新增样式、图标和主题 API 必须先核对本地 WPF-UI 4.3.0 程序集/XML 文档，必要时查该版本官方源代码，再编译和运行验证。Phase 0 不选定 FlaUI 包版本：Phase 1 先核实官方包的目标框架和 UIA3 依赖，固定 Core/UIA3 一致且经还原验证的版本，避免浮动版本。

## 3. 主窗口、页面与 MVVM

### 3.1 应用外壳

`App.xaml` 使用 `StartupUri="MainWindow.xaml"`。主窗口为 FluentWindow，Mica、圆角、扩展标题栏；初始 1280×960，最小 960×600。NavigationView 当前 `LeftMinimal`、展开宽度 150，Loaded 时关闭导航面板。没有统一全局状态栏。

`MainWindowViewModel` 在 C# 中创建四个主入口：自动轴机、脚本翻译、数据下载、视频压制；设置位于 footer。业务页 Required 缓存，设置页 Disabled 缓存。UIA 标识必须覆盖这些代码创建的导航项，不能只扫描 XAML。

`MainWindow.xaml.cs` 通过 `IAppPage<object>.OnNavigatedTo()` 初始化页面，并记录上次主模块；离开翻译页会 FlushDraft。更换导航布局时保留这些行为及页面实例生命周期。

### 3.2 绑定与交互

`ViewModelBase` 使用线程锁保护的字典及 INotifyPropertyChanged。现有交互同时包含绑定、Click 处理器、页面服务编排和静态单例，并非全部 ICommand。视觉改造应保留已有处理入口；不能以统一 Command 为理由扩大成业务重构。

设置由 `Configuration/settings-ui.json`、`SettingCatalog`、`SettingViewBuilder` 动态生成，另有自定义区块。新增资源和 AutomationId 必须同时进入生成器及自定义控件。设置类别目前为通用、轴机、下载、翻译、压制、开发者设置；保留相对顺序，新项和新类别按 `CLAUDE.md` 追加至末尾。

### 3.3 页面改造清单

| 页面与主要源码 | 当前基础 | 改造与回归重点 |
| --- | --- | --- |
| `View/Translate/TranslatePage.xaml`、`Components/TranslateLineDialog.xaml` | 文件工具栏、搜索与类型/状态筛选、统计、参考/差异选项；ListView；多处 Card | 先统一 PageHeader/Toolbar，减少重复容器；保留保存/另存为、撤销重做、同步、留空、TXT/TXT-v2、拖入、原声和参考差异；实测大列表滚动、焦点和离屏条目 |
| `View/Subtitle/SubtitlePage.xaml`、`Components/SubtitleTaskProcess.xaml` | 左侧 260、最小 220，右侧最小 420；分组队列、预览、结果及时间轴 | 增加可调分栏与时间轴高度/折叠，保留队列排序、恢复、导出、播放和定位；先验证小素材，避免把识别算法变化混入 UI |
| `View/Suppress/SuppressPage.xaml`、`Components/SuppressionTaskDetails.xaml` | 分组任务、详情、资源初始化和状态模型 | 统一任务工具栏、阶段/FPS/ETA/大小展示、日志搜索/复制/自动滚动与高级参数；逐项确认现有能力和缺口；缺少业务数据时另列功能工作，不伪造展示值 |
| `View/Download/DownloadPage.xaml` 及 Unit/Event/Special/Card/Action 组件 | 数据源、分类、部分虚拟化列表、下载队列 | 统一资源浏览工作区；补齐可调/可折叠队列、列表/网格状态契约；验证五类数据、选择、取消、失败重试及离线数据源 |
| `View/Setting/SettingPage.xaml(.cs)`、`SettingViewBuilder.cs` | 元数据驱动 CardExpander、动态编辑器、开发者区 | 标签与编辑器对齐、合理最大内容宽度、敏感值隐藏、展开/重置/保存重载；保持所有设置、排序和开发者入口 |

翻译页已有 `ScrollViewer.CanContentScroll=True` 和像素滚动配置，不能假定换容器后仍然虚拟化。字幕和压制页使用 PassiveScrollViewer 内的分组 ItemsControl，应实测队列数量增长时的容器和布局成本。

## 4. 主题与设计系统方案

当前 `App.xaml` 仅合并 WPF-UI ThemesDictionary、ControlsDictionary 及 `Converter/Converters.xaml`；没有项目级 DesignTokens、Light/Dark 或统一组件资源目录。页面已有大量 DynamicResource，可在此基础上演进。翻译页拖放错误区仍有硬编码红色、白色和局部圆角/间距，应迁移至语义资源。

`SettingPageModel.CurrentApplicationTheme` 的 0/1/2 分别调用 Light/Dark/HighContrast；值 3 的系统跟随实现目前注释。不能宣称跟随系统已实现；必须保留现有枚举含义，单列该行为的测试与处理。新增主题层不能破坏高对比度路径。

建议在 `SekaiToolsGUI/Resources/Design/` 建立以下结构，保持 WPF-UI 为基础控件提供者：

| 字典 | 责任 |
| --- | --- |
| `DesignTokens.xaml` | 4/8/12/16/24 间距、4–6 圆角、16/20/24 图标、32/36 控件高度基准 |
| `Light.xaml` / `Dark.xaml` | 相同语义键的颜色/画刷，不以反色算法生成浅色主题 |
| `Typography.xaml` | Segoe UI Variable 及 Segoe UI 回退；标题 20–22、分组 14–16、正文 13–14、辅助 11–12 DIP |
| `Buttons.xaml` / `Inputs.xaml` | 基于当前 WPF-UI 样式，覆盖悬停、按下、焦点、禁用和校验状态 |
| `Navigation.xaml` / `DataGrid.xaml` / `Editors.xaml` | 外壳、选择状态、虚拟化列表和编辑器样式 |
| `StatusIndicators.xaml` / `CommonTemplates.xaml` | 状态、空页面、任务项、PageHeader/PageToolbar 公共模板 |

画刷键采用 `AppBackgroundBrush`、`WorkspaceBackgroundBrush`、`SurfacePrimaryBrush`、`SurfaceSecondaryBrush`、`SurfaceHoverBrush`、`TextPrimaryBrush`、`TextSecondaryBrush`、`AccentBrush`、`AccentHoverBrush`、`BorderBrush`、`SuccessBrush`、`WarningBrush`、`ErrorBrush`。先检查与依赖资源的同名冲突，必要时统一增加 `Workbench.` 前缀并记录映射，避免无意覆盖框架资源。

深色初值按需求：背景 #191B20、工作区 #20232A、主表面 #252932、次表面 #2D323D、Hover #383F4B、边框 #3B414C、正文 #E6E9EF、辅助 #A5ADBA、强调 #65B9F7、强调 Hover #8ACBFF、成功 #55B987、警告 #E6B45C、错误 #E36C75。浅色建议从中性浅灰工作区、白色主表面、深色正文和较深强调色开始，实际值在 Showcase 对比度与截图检查后确定。

加载顺序为框架主题 → 框架控件 → 项目尺寸/字体/组件字典；项目 Light/Dark 字典由统一主题适配器替换，控件使用 DynamicResource。先在 Showcase 证明主题切换后的资源查找和焦点状态，再接入业务页面，避免每页自行切换主题。

外壳目标：导航展开约 200、折叠约 52 DIP；内容区统一标题/上下文/右侧操作；分栏使用 Grid 与 GridSplitter 并设置合理最小值；全局状态栏只展示实际可获得的任务摘要，不复制详细日志。Showcase 通过开发测试入口打开，不加入正式用户主导航。

## 5. AutomationId 审计与契约规划

静态扫描 GUI 的 **52 个非 bin/obj XAML 文件**：显式 `AutomationProperties.AutomationId` **0 行**，显式 `AutomationProperties.Name` **79 行**。GUI C# 检索也未发现 AutomationId 设置或自定义 AutomationPeer 实现。这是源代码统计，并不等于运行时 UIA 树中所有 AutomationId 都为空：WPF 可能从 x:Name 或模板提供标识，必须另做 UIA 运行扫描。

现有 Name 是可访问性基础，但含中文显示文本，不能作为长期定位契约。保留 Name，另外增加与语言无关的 ID。

| 区域 | 首批规划 ID（均未实施） |
| --- | --- |
| 外壳 | `MainWindow.Root`、`MainWindow.Navigation`、`MainWindow.StatusBar`、`MainWindow.DialogHost`、`MainWindow.Navigation.Translation/Subtitle/Encoding/StoryDownload/Settings` |
| 翻译 | `Translation.Root`、`LoadScriptButton`、`LoadTranslationButton`、`ReferenceButton`、`SaveButton`、`SaveAsButton`、`SearchBox`、`TypeFilter`、`StatusFilter`、`EntryList`、`NextUntranslatedButton`（后续键均加 `Translation.` 前缀） |
| 下载 | `StoryDownload.Root`、`SourceSelector`、`CategoryTabs`、`SearchBox`、`ChapterList`、`DownloadQueue`、`StartDownloadButton`、`CancelButton`（统一页面前缀） |
| 字幕 | `Subtitle.Root`、`TaskList`、`AddTaskButton`、`HistoryButton`、`VideoPreview`、`ResultList`、`PlayButton`、`TimelinePanel`、`ExportButton`（统一页面前缀） |
| 压制 | `Encoding.Root`、`TaskList`、`AddTaskButton`、`ProgressBar`、`FpsLabel`、`EtaLabel`、`LogViewer`、`AdvancedParameters`、`CancelButton`（统一页面前缀） |
| 设置 | `Settings.Root`、`Settings.Category.{StableKey}`、`Settings.Field.{Property}`、`Settings.Reset.{SectionKey}`、`Settings.TokenInput`；自定义控件单独列入目录 |
| Showcase | `Showcase.Root` 以及各真实组件与状态切换入口 |

动态行不能使用当前显示序号。`LineModel` 目前提供 Index、SequenceNumber，尚无独立长期 EntryId；Phase 1/2 核对原始剧本标识语义，采用“剧本身份 + 原始事件种类 + 原始事件身份”的复合键，筛选/排序不改变；没有原生 ID 时使用会话内固定身份映射并明确跨加载边界，不把显示索引包装成业务 ID。相同正文的两条事件必须仍可区分。

任务使用实际模型身份；下载已有运行期 Guid Id，可保证同一任务生命周期稳定，但跨运行不能用随机 Guid 命名视觉基准。列表切换分组、重排和虚拟容器回收时重新绑定 ID，不能保留旧容器 ID。

在 `SekaiTools.UITests/Automation/` 建立 Catalog（ID、页面、控件类型、场景、动态键策略、可用 UIA Pattern）。覆盖检查分两层：源码检查页面根、关键控件和生成器；真实 UIA 扫描检查当前已实现的场景、缺失/重复 ID 与 Invoke/Value/Selection/ExpandCollapse/Scroll 等能力。弹窗、折叠内容和虚拟化离屏条目分别触发后检查，不能把一次树扫描当作全覆盖。自定义 Popover、SelectorBar、时间轴、差异文本和预览优先列入 Pattern 审查，缺少可操作语义时补 Peer。

## 6. 启动、隔离与关闭的实际障碍

| 已确认的行为 | 对测试的影响与方案 |
| --- | --- |
| `App.OnStartup` 使用固定 Local 单实例 mutex | 同一桌面用户下不能随意启动第二个测试实例；发现正式实例时测试应报告环境冲突，不能附加或关闭它。首选专用测试账户/桌面会话 |
| 启动前检查未完成更新；窗口渲染后可能检查更新 | 独立构建目录不得含正式更新工作区；复用现有 `SEKAI_TOOLS_SKIP_UPDATE` 子进程环境变量，其值会在渲染后清除；它只跳过更新，不屏蔽其他网络 |
| `ResourceManager.DataBaseDir` 和 `BaseListStory.DataBaseDir` 分别基于 SpecialFolder.UserProfile | 仅设置工作目录或 USERPROFILE 字符串不构成可靠隔离；先在专用 Windows 测试账户运行，或评审后提供统一、显式的数据根配置接缝，生产默认路径保持不变 |
| 设置默认读写 `Data/setting.json`，EditorSessionStore 默认读写上次模块与翻译草稿 | 单个 SettingPageModel 构造器可注入路径，但不能隔离整个 App 静态单例；必须覆盖所有实际数据根后再执行保存/恢复测试 |
| 历史、队列断点、suppression-preferences、开发者阈值使用 ResourceManager 根 | 测试启动前断言路径均在隔离目录；不能备份后覆盖用户真实目录来模拟隔离 |
| 原声缓存使用 LocalApplicationData/SekaiTools | 也需独立根；普通 UI 回归使用本地固定音频或明确独立的网络场景，不依赖真实 CDN |
| 字幕页初始化资源并可能消费恢复记录；压制页检查 VapourSynth；下载页初始化远程源 | 新建空测试目录不足以离线打开全部页面；准备固定资源清单及小素材，通过配置的本地 fixture 服务返回已知内容；需要新增接缝时限制于组合根/配置，不更改业务规则 |
| 开发者服务和 Windows 通知随 App 启动 | 隔离配置默认关闭开发者模式；专用场景再开启并验证端口/令牌；通知测试避免把敏感内容写入截图 |
| `MainWindow.Closing` 先取消关闭，再异步确认翻译、FlushDraft、停止压制/字幕/下载及完成更新 | 使用真实窗口关闭并等待指定 PID 退出。未保存弹窗要显式处理。超时先保存诊断，再仅清理本次创建且身份匹配的进程及子进程，不能按进程名全局结束 |

Phase 1 首项交付必须证明隔离有效。专用账户方案可避免改动生产路径；若当前会话必须运行，则先实现最小的统一路径配置方案，并对 UserProfile、LocalAppData、临时文件、下载输出及更新目录逐项验收。该项属于测试宿主基础设施，不授权改变算法、持久化格式或接口协议。

## 7. FlaUI 项目与基础设施设计

新增根目录 `SekaiTools.UITests/`，与 `SekaiTools.Tests/` 平级，Windows TFM 与 GUI 对齐、x64，沿用 xUnit 2；FlaUI.Core 与 FlaUI.UIA3 固定一致版本。通过真实 exe 做进程外测试，避免测试代码进入 GUI 或用反射调用处理器替代 UIA 交互。

建议加入主解决方案用于构建，同时用运行入口明确区分业务测试和桌面测试。非交互环境的 UI 测试应明确 skip/环境不满足并在报告标明未验收；显式 UI 回归入口要求桌面就绪，不得静默产生“全通过”。使用单个非并行 xUnit collection，禁止多个测试同时争用桌面焦点。

| 目录/组件 | 责任 |
| --- | --- |
| `Fixtures/ApplicationFixture` | 启动自有 PID、绑定 UIA3、就绪状态、关闭与释放；不得把产品已有窗口当作测试成功 |
| `Fixtures/TestEnvironment` | 检查 Windows、交互桌面、会话、完整性级别、分辨率/DPI、正式实例冲突，记录 SDK/应用版本 |
| `Helpers/ElementFinder` | 在指定根按 AutomationId/Name/ControlType 查找，错误中包含查询条件和树上下文 |
| `Helpers/WaitHelper` | 有界条件轮询、取消与超时；等待页面根、加载完成、可用状态和属性，不以固定 Sleep 同步 |
| `Helpers/ScreenshotHelper` | 主窗口/指定窗口/页面截图，记录坐标和 DPI，截图失败不覆盖原始异常 |
| `Helpers/TestDataManager` | 每次运行唯一目录，复制固定样本和配置，验证输出根，成功清临时数据、失败保留必要现场 |
| `Helpers/FailureDiagnostics` | 首次失败即截图、标题、页面、目标 ID、目标/父级、UIA 树、堆栈和自有进程信息；限制树规模和耗时，脱敏令牌 |
| `Automation/Catalog` | 稳定 ID、类型与 Pattern 契约，动态 ID 构造规则 |
| `Tests/` | Startup、Navigation、TranslationEditor、StoryDownload、Subtitle、Encoding、Settings、Theme、Showcase |
| `TestData/` | Settings、Translation、StoryDownload、Subtitle、Encoding、Output 模板；小型、无账号、可重复素材 |

不能建立吞并所有职责的 FlaUIHelper。复用现有 `Tools/TranslationEditingProbe` 的场景与固定数据思路；它直接建立 WPF Application、操作模型/控件，不是独立进程 FlaUI 验收，保留其补充价值但不能替代 UIA 测试。

所有运行产物统一进入 `.codex-build/fluent-workbench/artifacts/{RunId}/`，包括 Screenshots、Failures、Reports、运行配置元数据和 fixture 服务日志；二进制复用任务构建目录，遵循构建产物保留规则。失败现场不得在重跑或清理时覆盖。

## 8. 测试、截图与报告计划

| 层级与标识 | 首批验收内容 |
| --- | --- |
| Smoke：UI-SMOKE-001、UI-NAV-001 | 真实 App 启动、主窗口/导航/五个页面根、加载完成、无错误弹窗、正常关闭 |
| Functional：UI-TRANSLATION-001 起 | 本地剧本与译文载入、多行编辑、焦点、保存/另存为、磁盘重读、搜索/筛选、未译跳转、撤销重做、差异、原声与错误反馈 |
| Functional：UI-DOWNLOAD-001 起 | 固定数据源五类内容、选择与队列、下载输出校验、取消、失败重试；不得执行不可控外部下载 |
| Functional：UI-SUBTITLE-001 起 | 创建小任务、选择/排序、预览/定位、结果编辑与导出、取消和恢复的独立样本；普通导航场景不运行长视频 |
| Functional：UI-ENCODING-001 起 | 队列、阶段/进度/日志、展开高级参数、取消；先用可控小任务，模拟仅用于展示状态，不替代真实小任务集成测试 |
| Functional：UI-SETTINGS-001、UI-THEME-001 起 | 生成器控件类型、NumberBox、保存重启重载、重置、敏感值、开发者模式、深浅/高对比主题；只写隔离配置 |
| Visual | 各页面默认、数据、选择、交互、弹窗、空状态及 Showcase；独立采集和回归，不能把功能通过当视觉通过 |

窗口目标为 1280×720、1280×960、1920×1080；明确区分 WPF DIP 和屏幕截图像素，记录实际窗口边界、显示器工作区及缩放。100%/125%/150% 在真实 Windows 缩放配置下分别运行，改变窗口大小不能模拟 DPI；高 DPI 时屏幕必须容纳相应逻辑工作区，条件不足列为未执行。

视觉输出包括原图、人工确认的基准、差异图、差异比例、容差/遮罩及人工复核结论。固定数据、主题、滚动和窗口位置；屏蔽时间/进度等动态区域或等待确定状态。容忍字体抗锯齿，但检查对齐、截断、溢出、焦点、层级和深浅对比度。基准更新是显式审核操作，失败截图绝不自动覆盖基准。

每例 JSON/Markdown 报告记录 TestId、名称、Category、开始/结束/耗时、步骤、预期/实际、错误、目标 ID、截图/树/日志路径和清理结果；TRX 供工具集成。环境不满足、未执行、测试失败、功能通过但视觉待复核必须区分。超时不自动重启丢弃第一次失败。

未来统一入口 `Tools/RunUITests.ps1` 计划支持 Build、Smoke、Functional、Visual、CaptureOnly、All、页面选择与输出目录。Category 使用 xUnit `[Trait("Category", "Smoke")]` 等，不将采集截图伪装为像素回归通过。以下是 Phase 1 实现后的目标命令，目前项目尚不存在：

```powershell
dotnet build SekaiTools.sln
dotnet test .\SekaiTools.Tests\SekaiTools.Tests.csproj
dotnet test .\SekaiTools.UITests\SekaiTools.UITests.csproj --filter "Category=Smoke"
dotnet test .\SekaiTools.UITests\SekaiTools.UITests.csproj --filter "Category=Functional"
dotnet test .\SekaiTools.UITests\SekaiTools.UITests.csproj --filter "Category=Visual"
dotnet test .\SekaiTools.UITests\SekaiTools.UITests.csproj --filter "FullyQualifiedName~TranslationEditorTests"
dotnet test .\SekaiTools.UITests\SekaiTools.UITests.csproj
```

## 9. 分阶段执行及退出条件

| 阶段 | 交付顺序 | 必须满足的退出条件 |
| --- | --- | --- |
| Phase 0（本文） | 核实实际依赖、页面/主题/MVVM、自动化缺口、隔离风险与实施设计 | 输出可追溯审计；明确静态结论和运行待验收边界 |
| Phase 1A | 隔离环境与真实 App 生命周期；FlaUI 项目、首批外壳/页面根 ID；失败诊断 | 证明不触碰用户数据、离线就绪、真实启动/一次导航/关闭成功、一次失败能保留截图和树；重复运行无残留 |
| Phase 1B | Tokens、Light/Dark、公共样式/模板、开发 Showcase、主题适配器、Catalog 检查 | 编译、Smoke、Showcase 交互和截图检查通过；焦点/禁用/高对比不退化；基准人工确认 |
| Phase 2 | 翻译编辑器试点；动态行 ID；本地 fixture 与编辑/保存/筛选测试 | UIA 实际输入保存并重读、虚拟化/多行/大样本检查、深浅截图复核及业务回归；确定后续可复用规范 |
| Phase 3 | 字幕 → 压制 → 下载 → 设置；每页独立提交和回归 | 每页真实运行、Smoke+当前页 Functional、截图审查修复后重测，保留既有绑定/流程与输出格式 |
| Phase 4 | 全页面、双主题、尺寸/DPI、视觉与业务完整回归 | 分开报告自动化/视觉/环境缺口；全部必需矩阵具备实际证据，不能用编译补足 |
| Phase 5 | 设计系统/组件说明、ID Catalog、运行手册、报告/截图索引、已知问题 | 可从独立测试账户复现，产物路径清晰，未完成项具名列出 |

每个 UI 改动循环固定为设计 → 实现 → 构建 → 真实启动 → Smoke → 当前页 Functional → 截图检查 → 修复 → 相关回归。C# 按仓库要求执行 dotnet format 和 verify-no-changes；独立功能通过对应验证后按中文 Conventional Commits 提交。

## 10. 技术风险与处理优先级

| 优先级 | 风险 | 处理与验证 |
| --- | --- | --- |
| P0 | 用户设置、草稿或恢复记录被消费/覆盖 | 隔离数据根或专用账户先行；路径断言；禁止直接测试正式实例 |
| P0 | 导航触发外部资源下载导致不确定性 | 本地 fixture 服务、固定合法资源清单/缓存；明确网络场景独立执行 |
| P1 | mutex、恢复更新、异步关闭影响启动退出 | 自有进程与独立安装目录；现有 skip-update；退出状态等待和失败留证 |
| P1 | 模板或自定义绘制不暴露 UIA Pattern | 真实 UIA 树与键盘操作先验证，需要时补 Peer，不用坐标绕过 |
| P1 | 动态设置和虚拟列表 ID 缺失/复用 | 生成器统一赋值、业务身份复合键、过滤/重排/回收场景测试 |
| P1 | 项目资源与 WPF-UI 主题优先级冲突 | Showcase 先验证；DynamicResource；双主题/高对比截图与状态检查 |
| P1 | 重写列表或任务面板破坏焦点、虚拟化和关闭保存 | 翻译试点、固定大样本、UIA 编辑保存、未保存关闭/任务取消测试 |
| P1 | 无交互桌面、权限不同或屏幕不足 | 测试入口预检；报告阻塞和未运行；CI 使用专用交互 Windows runner |
| P2 | 需求中的日志/列表网格等能力超出现有 UI | 逐项盘点缺口；新增功能单独评审和测试，不在样式重构中改业务协议 |
| P2 | 发布 DLL 整理与原生库路径不同于 Debug | 基础回归用标准构建；另保留真实 Publish 布局的启动/原生依赖验收 |

Phase 0 未建立视觉基准，也未验证当前桌面的 UIA/截图权限。下一实施动作应从 Phase 1A 的隔离与启动闭环开始，再扩展设计系统和页面；不应直接批量重写 XAML。
