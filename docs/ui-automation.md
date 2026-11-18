# Windows UI 自动化测试

`UI-TRANSLATION-005` 验证当前/参考TXT载入、三行真实按键及Tab、两类差异独立开关、三主题状态保持、覆盖保存后载入基准不变。Translation.LoadReferenceButton为参考入口；Translation.Dialog.{SequenceNumber}.Speaker/Reference/Loaded分别为角色输入、只读参考和载入快照，TargetText保留原正文契约。指定编辑框的黄色/蓝色像素采样只作差异语义回归，不代表全页像素基准或系统高对比验收。

`UI-TRANSLATION-004` 验证实际地点文字同步开关、非立即覆盖、同步整体撤销/重做，以及三主题×两尺寸的筛选与选项浮层。Translation.OpenEditorOptions为入口，Translation.Option.ShowReference/HighlightReference/HighlightModified/SyncContent/SyncSpeakers/AutoFormat为六个真实CheckBox，支持Toggle；Popup通过同PID桌面子树定位，Esc关闭。Translation.TypeFilter支持Selection，Translation.NextUntranslated/NextWarning支持Invoke；地点输入框的稳定ID为Translation.Effect.{SequenceNumber}.TargetText。

`UI-TRANSLATION-003` 使用300条固定对话验证真实长篇滚动、离屏编辑保持、撤销重做、Ctrl+F筛选及完整顺序导出；Translation.EventList为原生ListView的Scroll定位入口。输出translation-performance.json和固定截图，改造前观察值及解释见 [M2进展](fluent-workbench-m2-progress.md)。

外壳按用户复核移除独立模块标题/说明横栏。UI-DESIGN-002/005保留真实导航、日志开关、三主题/三尺寸，并改为检查工作区紧接窗口标题栏、完整利用窗口高度；MainWindow.ToggleNavigation/OpenLogs现位于导航PaneHeader/PaneFooter。MainWindow.PageHeader契约已移除。以下旧页头截图记录仅描述调整前的批次。

`UI-DESIGN-006` 经真实预览入口在三主题下操作 WorkbenchSplitView：键盘方向键与鼠标拖动改变主编辑宽度，800×600收起辅助区、焦点移回主编辑区并保持完整可见，恢复1280×960后核对内容与调整后的宽度。Workbench.Splitter 是原生 GridSplitter，支持键盘焦点与左右键；Showcase.Split.Editor/Reference 是预览输入框，支持 Value/Text。保存聚焦、紧凑、恢复截图与 split-matrix.json。

`UI-DESIGN-005` 经真实设置页切换亮色、暗色、高对比度，组合960×600、1280×960、1920×1080主窗口，检查页头、导航开合、操作边界及日志窗口打开/关闭，保存18张截图和 shell-matrix.json。明暗页头同时采样实际工作区背景。测试仅覆盖公共外壳，不将空翻译页截图作为后续业务页面重构验收。

`UI-DESIGN-004` 在三种应用主题下切换实际 DownloadTask 的等待、下载中、完成、失败、停止、跳过六种状态，核对状态文字、失败原因清除和禁用重试，保存18张截图与 task-state-matrix.json。Showcase.TaskState 是预览选择器，StoryDownload.Task.Status/Error 是实际任务组件的状态/错误文字。截图采样检查状态标签最强字形像素相对邻近底板的对比度至少4.5:1；这是指定组件的回归检查，不代表完整无障碍审计。

`UI-DESIGN-003` 经实际预览入口组合三种应用主题与四个窗口尺寸（800×600、1280×720、1280×960、1920×1080），检查长标题换行、首行图标像素、滚动后错误提示、编辑选择保持和键盘焦点。每个场景记录三张截图与 layout-matrix.json，采用当前96 DPI的物理像素尺寸。Showcase.LongTitle 为长标题开关，Showcase.Scroll 为实际滚动容器。未切换系统DPI或系统高对比。

`UI-DESIGN-002` 验证公共外壳页头随真实导航更新，并在960×600窗口通过 MainWindow.ToggleNavigation 展开/收起导航、通过 MainWindow.OpenLogs 打开现有日志窗口。MainWindow.PageHeader 下的 Workbench.Header.Title 是当前标题契约；运行日志根为 RuntimeLog.Root。UI-DESIGN-001 另在800×600预览检查主操作可见及公共工具栏实际折行。相关状态和未执行矩阵见设计系统说明。

M1 新增 `UI-DESIGN-001` 组件预览用例：在隔离实例按 Ctrl+Alt+F12 打开 Showcase，检查三种应用主题的编辑、实际翻译行、选择、禁用、校验、焦点、背景像素及关闭恢复原主题，并保存截图。入口、资源与验证边界见 [设计系统说明](fluent-workbench-design-system.md)。应用高对比选项不等同于已经验证 Windows 系统高对比。

## 当前范围

`SekaiTools.UITests` 是独立于业务测试的进程外 FlaUI/UIA3 工程，使用 xUnit 2、FlaUI.Core/UIA3 5.0.0、与 GUI 对齐的 Windows .NET 10 目标框架和 x64。实施顺序见 [Fluent Workbench 方案](fluent-workbench-implementation-plan.md)。

M0-A 用例包括：

- `UI-SMOKE-001`：连续三轮真实启动，确认五个导航入口，打开设置并返回翻译空工作区，采集稳定截图，正常关闭应用。
- `UI-INFRA-001`：故意查找不存在的 AutomationId，验证超时、窗口截图、UIA 树和结构化诊断文件，再正常退出。这是诊断系统自检，不能代表业务错误场景已全部覆盖。

M0-B 首批功能用例 `UI-SETTINGS-001` 通过真实 ComboBox 切换暗色、关闭并启动新进程，验证自动保存后的主题重载，再切回亮色；采集两个主题的翻译空工作区截图。动态生成的 choice 控件使用 `Settings.Field.{Property}` 标识。

该用例继续通过 Settings.ResetAll 验证全局重置：取消确认后配置文件 SHA-256 和暗色选择保持不变，接受确认后恢复默认亮色。此检查不代表每个配置字段均已逐项核对。

UI-SETTINGS-002 通过 Settings.VersionLabel 聚焦后按七次 Enter 开启现有开发者模式，展开 Settings.DeveloperGroup。Settings.Threshold.{属性名} 标识六个阈值输入；Save、Reset、Status 分别对应独立保存、恢复来源和反馈。验证零值拒绝、有效值编辑不自动保存、点击保存后落盘、非法覆盖不改文件哈希、重启后恢复已保存值以及恢复默认来源删除覆盖文件。测试不预装识别资源，同时覆盖缺少 thresholds.json 时使用内置默认值；损坏的已有文件仍报告错误。设置页不是启动恢复模块，重启后重新导航进入设置。该场景不代表所有数值边界、全部普通设置或 HTTP API 的完整验收。

默认 M0-A 配置仅指向本地未提供业务响应的端口；业务用例按需使用下面的 HTTP fixture。当前不执行字幕识别或编码。其他业务页、完整主题/尺寸/DPI 矩阵、视觉差异比较属于后续里程碑。现有 WPF 探针仍作补充，不代替 FlaUI。

## 运行

首次还原依赖：

```powershell
dotnet restore SekaiTools.sln
```

使用可交互、未锁定的 Windows 桌面，关闭正在运行的正式 SekaiTools 实例后执行：

```powershell
pwsh -NoProfile -File Tools/RunUITests.ps1
# 使用已构建的独立测试应用
pwsh -NoProfile -File Tools/RunUITests.ps1 -SkipBuild
# 功能测试或当前全部用例
pwsh -NoProfile -File Tools/RunUITests.ps1 -Mode Functional
pwsh -NoProfile -File Tools/RunUITests.ps1 -Mode All
# 只运行诊断自检
pwsh -NoProfile -File Tools/RunUITests.ps1 -Filter "FullyQualifiedName~UI_INFRA_001"
```

脚本构建 Release GUI 至 `.codex-build/fluent-workbench/app/`，构建测试项目后显式启用 UI 测试。`-Mode All` 运行当前项目已有的全部用例，不表示后续业务与视觉用例已实现。无匹配用例或存在未执行用例时，入口不能作为 UI 验收通过。

普通 `dotnet test SekaiTools.sln` 默认跳过桌面用例，并明确记录未执行，避免普通单元测试意外操作桌面。业务回归可以独立运行：

```powershell
dotnet test SekaiTools.Tests/SekaiTools.Tests.csproj
```

显式 UI 运行在桌面不可用、权限不足或已有实例时失败并给出原因，不附加到正式实例。UI 测试不并行执行。沙箱若阻断测试宿主命名管道或 UIA，应在具备相应权限的交互会话执行，不能将未执行写成通过。

## 数据隔离

测试子进程通过 `SEKAI_TOOLS_TEST_ROOT` 使用每次运行独立的绝对路径。`ApplicationPaths` 要求有效标记文件，拒绝正式数据目录及其祖先/子目录，拒绝根路径、父目录和现有子内容中的链接。无此环境变量时，正式目录保持不变。

| 内容 | 隔离方式 |
| --- | --- |
| 资源、历史、队列检查点、设置、草稿、压制偏好、开发者阈值 | 统一进入测试根下的 `DataRoot`；ResourceManager 和 BaseListStory 共用路径入口 |
| 原声缓存 | 测试根下的 `LocalRoot/VoiceCache`；正式运行仍使用 LocalAppData/SekaiTools/VoiceCache |
| 临时文件及压制临时区 | 仅为子进程设置 TEMP/TMP；App 验证实际 `Path.GetTempPath()` 在测试根内 |
| 默认下载输出 | 由隔离数据根派生；后续文件测试仍须验证用户选择路径的边界 |
| 网络配置 | 测试根中的 `network-endpoints.json` 必须存在，仅允许 loopback；不回退嵌入的正式配置 |
| 更新及通知 | 子进程使用既有跳过更新机制；测试构建目录独立，隔离启动不注册 Windows 通知 |

启动前设置独立的上次模块记录，使应用通过正常会话恢复路径进入翻译空工作区，不绕过正式导航初始化。应用输出 `resolved-paths.json`，测试核对实际资源、下载、语音与临时路径；前后比较正式数据目录的路径/大小/修改时间指纹。这是路径与文件元数据检查，不是操作系统级文件访问审计。

成功后清理测试临时目录和自有 Resource 下载缓存，保留固定资源包、配置、输入输出、解析路径及报告以便复现；失败保留必要现场。测试结束走真实关闭流程，超时先采集诊断，再仅清理本次创建的进程树。不存在按进程名结束其他实例的逻辑。

## AutomationId 与控件行为

首批契约如下；本表中未被当前 Smoke 打开的页面根，只表示已在源码赋值，不能算已完成该页面交互验收。

| 标识 | 用途 |
| --- | --- |
| `MainWindow.Root` | 按自有进程识别真实主窗口 |
| `MainWindow.Navigation` | 主导航与工作区容器 |
| `MainWindow.Navigation.Subtitle/Translation/StoryDownload/Encoding/Settings` | 五个代码创建的导航项，后缀分别使用，不能把斜杠串作为一个 ID |
| `Translation.Root`、`Settings.Root` | 首批导航就绪条件 |
| `Subtitle.Root`、`StoryDownload.Root`、`Encoding.Root` | 后续场景的页面根契约 |
| `Translation.LoadScriptButton` | 翻译空工作区可操作性检查 |
| `MainWindow.DialogHost` | 弹窗宿主的源码标识；运行时需以实际弹窗 Peer 为定位依据 |

WPF-UI 4.3.0 的 NavigationView 本身不提供容器 Peer，因此使用保持原样式的 `AccessibleNavigationView` 暴露 Pane。导航项由框架 ListBox 的 DataItem Peer 暴露，当前不提供 Invoke/SelectionItem；测试按稳定 ID 定位、聚焦后使用 Enter 激活，再验证目标页面；导航前等待静态窗口画面稳定，不使用固定屏幕坐标，也不直接调用产品处理器。完整屏幕阅读器语义仍需独立验证。

新增重要控件时扩展 `Automation/Catalog.cs` 和对应测试。不可把 x:Name 的偶然回退视为已建立显式契约；动态行和任务的稳定身份策略在各页改造前落实。

主导航沿用 WPF-UI 原始 peer，ItemStatus 通过绑定真实 IsActive 提供“当前页面”状态，未选中为空。共用 Navigate 同时检查目标页面根、目标选中态及其他导航项未同时选中。此状态不改变原导航顺序和样式，也不代替高亮外观检查。

## 报告、截图和诊断

每轮输出至 `.codex-build/fluent-workbench/artifacts/{RunId}/`，包括 TRX，以及每个独立实例的：

- `report.json`：用例 ID、步骤、期望/实际、耗时、失败原因、清理结果和正式目录指纹比较。
- `environment.json`：会话、桌面、系统 DPI、屏幕、窗口范围、实际 exe、PID/启动时间和隔离路径。
- `Screenshots/`：当前固定场景截图。通过连续帧一致性等待导航动画完成；此方法只用于静态场景，不能直接用于持续变化的进度或视频画面。
- `Failures/` 与诊断 JSON：首次失败的截图、控件树、目标与父级信息（存在时）、窗口、异常及诊断采集错误。树遍历有数量/深度/时间限制，缺少目标时仍保留当前树。
- `ExpectedFailure/`：诊断自检的预期超时现场。
- `application.log`：测试子进程 UTF-8 输出；Bearer 信息脱敏。

失败截图立即采集，不等待页面稳定，也不会被重跑覆盖。没有人工确认的视觉基准时，截图采集成功不等于视觉回归通过。当前实际运行验证仅覆盖 100% 系统缩放下的最小闭环；其他显示配置单独记录未执行。

## 翻译文件基线

UI-TRANSLATION-001 使用合成单条剧本，通过原生文件选择器载入、UIA 输入正文、Ctrl+S 及保存选项导出，再从磁盘解析 TXT v2 并核对条目数和译文。所有样本和输出均在隔离根内。Translation.Dialog.{SequenceNumber}.TargetText 使用当前已载入剧本的原始事件顺序，非筛选后的显示索引；身份仅在该载入会话内有效。Translation.SaveOptions 标识导出弹窗。同一用例继续验证覆盖取消后原文件 SHA-256 不变、未保存编辑保留，以及搜索隐藏条目后另存为校对副本仍完整导出；副本重新解析核对正文，原文件哈希保持不变，清空搜索后条目重新显示。多条目组合筛选由下述 UI-TRANSLATION-002 覆盖，完整导入矩阵仍待后续覆盖。新增契约为 Translation.SearchBox、Translation.SaveAsButton 和共享的 Export.OverwriteConfirmation。

UI-TRANSLATION-002 使用三条独立对话，组合未译状态与原文搜索，验证原始第二条在筛选后仍使用同一 ID、译后即时退出未译列表、恢复全部状态后保留编辑。筛选显示第三条时保存全篇，再从磁盘解析核对三条正文及原剧本顺序，未译第一条沿用原文。新增 Translation.StatusFilter 契约。该小样本验证身份和数据行为，不代表长列表虚拟化或滚动性能验收。

该用例在导出前导航至设置，解析实际离页草稿的 ScriptPath 和 Values，核对第二、第三条译文；返回后核对搜索词及正文仍保留。UI-TRANSLATION-001 另通过 Translation.LoadTranslationButton 和原生文件选择器载入普通 TXT，接受现有替换确认后核对正文，源 TXT 哈希不变。此处仍不代表重启草稿恢复或复杂导入矩阵。

## 资源失败恢复基线

UI-RESOURCE-001 在隔离环境启动仅绑定 loopback 随机端口的 HTTP fixture，向真实字幕页和压制页资源请求返回 503。分别验证错误对话框、检查设置入口、正常退出，并记录请求与失败场景截图。这是资源失败与恢复导航验证，不代表资源准备成功、识别或编码功能验收。fixture 随用例关闭，不访问正式资源或外部服务。真实字幕资源清单、合成视频识别和压制管线由下述正常路径覆盖。

## 下载页与受控失败基线

UI-DOWNLOAD-001 使用 LoopbackHttpFixture 提供有效数据源列表及单条主线剧情数据，通过真实刷新按钮加载条目、搜索无匹配内容并恢复。新增 StoryDownload.SearchBox、StoryDownload.RefreshButton 和 StoryDownload.EmptyMessage 契约。请求日志记录实际本地请求；未映射路由返回503，避免意外成功。该场景不等同于五类完整内容；其余分类的非空内容 fixture 仍需继续交付。

下载基线进一步通过 UIA 勾选/取消全选、加入队列和开始下载，获取本地合成剧本并核对隔离 Scripts 目录中的实际文件、原始响应内容和解析后的 ScenarioId/对白。清空队列后再次核对文件仍存在且内容不变。新增 StoryDownload.SelectResults、AddSelectedButton、StartDownloadButton、ClearQueueButton（均带 StoryDownload. 前缀）。全选逻辑响应 Checked/Unchecked，界面刷新回写状态时使用保护标志避免递归误选；鼠标、键盘和 UIA 共用选择规则。

同一用例先为资产请求返回503，验证下载失败与目录无残留；通过 StoryDownload.Task.RetryButton 重试后，fixture 在响应头发送前等待有界信号，确认实际请求已到达，再使用主按钮停止下载，验证已停止及目录仍无残留。释放响应后再次重试，正常获取并解析剧本。重试 ID 在单个任务容器内唯一；本用例仅有一个任务。报告包含失败、停止及成功截图和请求序列。该场景覆盖等待服务器响应时取消，不代表响应正文传输中取消、多任务并发、覆盖或各种HTTP状态矩阵。

下载基线已扩展至五类切换：活动、特殊、角色及地图对话通过正常 HTTP 200 返回空数组，实际刷新各类数据并确认空结果；搜索词跨分类保持，返回主线可恢复非空样本并继续下载。分类入口契约为 StoryDownload.Category.Unit/Event/Special/Card/Action，内容根为 StoryDownload.Unit/Event/Special/Card/Action.Root（各后缀分别使用）。这些空数组仅验证对应分类空数据加载，不代表其非空业务内容、地图活动关联或完整元数据矩阵已验收。

## 真实字幕资源包

UI-SUBTITLE-001 校验四个锁定版本文件，经本地 HTTP 触发真实 ResourceManager 下载与 MD5/大小校验，再独立比较落盘 SHA-256，验证添加任务表单可打开、取消和退出。该场景不执行视频识别或 ASS 导出，不能当作算法或真实视频流程验收。

UI-SUBTITLE-002 用固定资源生成三秒 1280×720/24 FPS 合成对话视频，通过真实文件选择器自动匹配同名 JSON/TXT、加入常规识别队列并导出 ASS。验证单条 Dialogue 的译文、起止时间接近素材的 0.5～2.5 秒（容差 0.1 秒）及源视频 SHA-256 不变；保留素材、ASS、识别区间、应用日志和完成截图。新增 Subtitle.VideoBrowseButton、Subtitle.ExportButton、Subtitle.ExportOptions 契约。素材复用模板渲染器，因此它验证界面到真实处理器和导出的流程，不是独立的实际录屏准确率基准；横幅、标记、恢复及实验性仅视频模式仍需另行覆盖。该用例同时需要下述两个固定资源包。

隔离测试模式下 ExplorerHelper 不打开或聚焦全局资源管理器，避免 ASS 导出干扰其他桌面窗口；实际 ASS 写入和识别处理照常执行。此模式不覆盖导出后的 Windows Shell 集成。

UI-SUBTITLE-002 继续关闭并启动独立进程，验证应用恢复上次字幕页，通过 Subtitle.HistoryButton / Subtitle.HistoryDialog 加载首次真实识别生成的历史记录。移开第一次导出的 ASS 后再次导出，核对全部 Dialogue 行与首次结果一致、历史 JSON 的 SHA-256 和条目数不变，并采集历史任务分组截图。这覆盖已完成历史的读取与复用，不代表未完成队列的断点续跑或取消恢复验收。测试宿主允许显式指定预期启动页，默认仍验证翻译工作区，不改写应用保存的最近模块。

UI-SUBTITLE-003 将同一合成对话延长至120秒，经真实终止按钮取消，读取实际检查点确认已推进且未到片尾；重启后接受“恢复未完成任务”提示，继续完成识别并导出 ASS，核对单条译文与完整起止时间，以及恢复记录已消费。新增 Subtitle.StopButton 契约。报告保留取消帧、前后截图和日志中的恢复帧；不注入 ProcessingState 或改写进度。该场景覆盖一次取消后的正常恢复完成，不代表恢复后再次取消、放弃恢复、多任务排序或异常退出矩阵。

Functional/All 的完整运行需先准备固定资源包：运行 Tools/PrepareUIVideoResources.ps1 -SourceDirectory <合法取得且版本匹配的videoProcess目录>。脚本先核对 TestData/video-resources.lock.json 的全部 SHA-256，再复制至 .codex-build/fluent-workbench/fixtures/videoProcess；不修改源目录，也不把字体文件提交到仓库。测试只读该副本，不依赖用户正式资源路径。缺失或版本不匹配会明确失败，不跳过验收；Smoke 不需要这个包。

新增 Subtitle.AddTaskButton 和 Subtitle.AddTaskDialog 契约。LoopbackHttpFixture 已支持二进制响应，并在响应前消费有界请求头，仍仅监听本地随机端口；未映射路由继续返回503。

## 真实压制运行库与转码基线

UI-ENCODING-001 使用 media-resources.lock.json 锁定的83个运行库文件。运行 Tools/PrepareUIMediaResources.ps1 -SourceDirectory <合法取得且版本匹配的vapourSynth目录> 先校验全部SHA-256再复制到 .codex-build/fluent-workbench/fixtures/vapourSynth；源目录不修改，二进制不入库。Functional/All 现在需要视频与压制两套资源包，缺失明确失败。

测试使用固定FFmpeg生成两秒320×240/24 FPS无音轨合成视频，经真实页面资源准备、原生文件选择、后台媒体分析和添加任务进入自动队列。验证已完成状态、隔离输出存在、源文件哈希不变，并用独立FFmpeg解码输出，核对48帧。该用例经真实VSPipe与FFmpeg管线完成仅转码，日志和输出解码提供运行证据；不代表ASS烧录、取消、音轨或覆盖输出矩阵验收。请求日志、生成及解码日志、完成截图均留在实例报告目录。

新增 Encoding.AddTaskButton、Encoding.AddTaskDialog、Encoding.VideoBrowseButton 契约；原生文件选择定位检查文件名Edit，避免误选嵌套的WPF任务表单。

UI-ENCODING-002 复用真实转码链路，在输入目录准备同名ASS，确认任务自动采用并实际烧录。字幕使用无字体依赖的绿色绘图矩形；独立FFmpeg从输入/输出中心32×32区域提取平均RGB，验证蓝色输入变为绿色输出，同时确认视频及ASS源文件哈希未变、输出48帧。报告含数值、解码日志和烧录后帧PNG。该场景验证绘图字幕进入画面，不等同于多字体文字、特效、音轨或所有分辨率验收。

UI-ENCODING-003 生成十分钟320×240合成视频，等待隔离 TEMP 下真实 FFmpeg 临时视频开始写入，再通过 Encoding.CancelButton 取消。验证已取消状态、Encoding.TaskLog 的取消记录、源视频 SHA-256 不变、临时视频删除且目标输出未发布。报告包含取消日志、临时文件路径、清理结果和截图。此用例沿用真实输出事务，不能等待最终目标出现才取消，也不把取消结果当作成功转码；仍未覆盖音轨、多任务取消、覆盖已有输出或强制终止矩阵。
