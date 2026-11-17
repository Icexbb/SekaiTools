# Windows UI 自动化测试

## 当前范围

`SekaiTools.UITests` 是独立于业务测试的进程外 FlaUI/UIA3 工程，使用 xUnit 2、FlaUI.Core/UIA3 5.0.0、与 GUI 对齐的 Windows .NET 10 目标框架和 x64。实施顺序见 [Fluent Workbench 方案](fluent-workbench-implementation-plan.md)。

M0-A 用例包括：

- `UI-SMOKE-001`：连续三轮真实启动，确认五个导航入口，打开设置并返回翻译空工作区，采集稳定截图，正常关闭应用。
- `UI-INFRA-001`：故意查找不存在的 AutomationId，验证超时、窗口截图、UIA 树和结构化诊断文件，再正常退出。这是诊断系统自检，不能代表业务错误场景已全部覆盖。

当前不执行字幕资源初始化、下载列表刷新、编码、文件编辑或保存；固定端点仅指向本地未提供业务响应的端口，不是完整 fixture 服务器。其他业务页、明暗主题、尺寸和 DPI 矩阵、视觉差异比较属于后续里程碑。现有 WPF 探针仍作补充，不代替 FlaUI。

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

成功后清理测试临时目录，保留配置、解析路径及报告以便复现；失败保留必要现场。测试结束走真实关闭流程，超时先采集诊断，再仅清理本次创建的进程树。不存在按进程名结束其他实例的逻辑。

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

WPF-UI 4.3.0 的 NavigationView 本身不提供容器 Peer，因此使用保持原样式的 `AccessibleNavigationView` 暴露 Pane。导航项由框架 ListBox 的 DataItem Peer 暴露，当前不提供 Invoke/SelectionItem；测试按稳定 ID 定位后使用 FlaUI 元素点击，再验证目标页面。点击位置由元素当前几何范围取得，没有固定屏幕坐标，也不直接调用产品处理器。后续键盘/屏幕阅读器语义应独立验证。

新增重要控件时扩展 `Automation/Catalog.cs` 和对应测试。不可把 x:Name 的偶然回退视为已建立显式契约；动态行和任务的稳定身份策略在各页改造前落实。

## 报告、截图和诊断

每轮输出至 `.codex-build/fluent-workbench/artifacts/{RunId}/`，包括 TRX，以及每个独立实例的：

- `report.json`：用例 ID、步骤、期望/实际、耗时、失败原因、清理结果和正式目录指纹比较。
- `environment.json`：会话、桌面、系统 DPI、屏幕、窗口范围、实际 exe、PID/启动时间和隔离路径。
- `Screenshots/`：当前固定场景截图。通过连续帧一致性等待导航动画完成；此方法只用于静态场景，不能直接用于持续变化的进度或视频画面。
- `Failures/` 与诊断 JSON：首次失败的截图、控件树、目标与父级信息（存在时）、窗口、异常及诊断采集错误。树遍历有数量/深度/时间限制，缺少目标时仍保留当前树。
- `ExpectedFailure/`：诊断自检的预期超时现场。
- `application.log`：测试子进程 UTF-8 输出；Bearer 信息脱敏。

失败截图立即采集，不等待页面稳定，也不会被重跑覆盖。没有人工确认的视觉基准时，截图采集成功不等于视觉回归通过。当前实际运行验证仅覆盖 100% 系统缩放下的最小闭环；其他显示配置单独记录未执行。
