# macOS 首版移植

## 已确认范围

- 仅 Apple Silicon，发布目标 `osx-arm64`；不构建 Intel 或 Universal Binary。
- SwiftUI 原生界面，最低 macOS 15，覆盖 macOS 26。
- 优先轴机、翻译、下载。不提供视频压制入口，不下载 VapourSynth 压制资源。
- 保留 C# 剧本、翻译、识别和 ASS 生成规则，Swift 不重复实现这些规则。

## 运行结构

`SekaiToolsMac` 是 Swift Package 可执行程序；`SekaiToolsHost` 是独立 .NET 命令行程序，不引用 WPF。
每次操作启动一个 Host 子进程，通过 UTF-8 JSON Lines 交换请求、进度和结果。一次进程仅执行一个请求，避免不同数据源、翻译文档和视频处理器共享可变状态。
标准输出仅输出协议；业务日志重定向到标准错误。协议版本为 1。

首行请求包含 `version`、`command` 和 `args`。响应类型为 `progress`、`result` 或 `error`。
轴机运行期间，输入第二行 `{"command":"cancel"}` 请求停止并等待检查点保存；不得直接杀死进程代替正常取消。
输入 EOF 同样请求停止，防止前端异常退出后识别继续占用资源。

## 首个实现切片与完整首版边界

| 模块 | 首个实现切片 | 完整首版仍需补齐与验收 |
| --- | --- | --- |
| 轴机 | 视频/剧本/翻译选择、进度与停止、显式恢复、结果列表、ASS 导出、内嵌视频播放 | 多任务队列、历史入口、时间轴编辑、逐帧预览同步、每次输出的样式选择 |
| 翻译 | 剧本加载、旧/v2 翻译导入、逐条角色与正文编辑、v2 保存、未保存提示 | 参考翻译、自定义角色、特殊字符、旧格式导出、批量联动编辑 |
| 下载 | 配置内数据源、活动/卡片/主线/特殊/区域剧本目录、搜索、单项保存 | 模块专属筛选、批量下载、TalkId 筛选输出、代理设置 |

上表第二列不是完整首版验收。尚未完成的功能不能在 UI 中展示为已实现。
下载条目保留服务端标识；输出文件通过现有 `GameScript` 解析验证后才写入。
翻译导出按当前剧本重新校验行数、类型和标识，文件原子替换，失败不清除未保存状态。

## 原生依赖门槛

已解包核查 `Emgu.CV.runtime.mini.macos/5.0.0.6584`：含 ARM64 与 x64 库；包含 core/imgproc/imgcodecs/highgui/videoio；AVFoundation=YES，FFmpeg=NO。
两份 Mach-O 的 `LC_BUILD_VERSION` 都将最低系统标为 27.0。这与 macOS 15/26 目标不兼容。
**不得通过修改 Mach-O 版本字段宣称兼容。** 需要用 macOS 15 deployment target 重建 ARM64 cvextern/OpenCV，或获得供应方兼容包；随后在 15 和 26 分别实测。

开发包可用于下载和翻译；轴机在 macOS 15/26 上保持禁用，直到显式配置并验证替代原生库。
Host 的 `capabilities` 在不加载 OpenCV 的前提下报告运行环境。替代库通过 `SEKAITOOLS_CVEXTERN_PATH` 指定，必须为绝对路径；这只是开发注入点，不等于兼容性验收通过。
周期彩色捕获 Windows 插件不进入 macOS 首版。字体模板渲染依赖 SkiaSharp，也需在目标设备验证。

## 验证要求

1. Windows：Host 构建、协议错误处理与翻译往返测试，不证明 macOS 原生运行。
2. Mac：SwiftUI 编译、窗口与文件面板、中文输入、未保存退出、子进程关闭与错误处理。
3. macOS 15/26 ARM64：原生库加载、字体渲染、真实 H.264/HEVC 样本读取、帧数/时间戳/定位、取消恢复、ASS 与 Windows 对照。
4. 分发：ARM64 .app 包、签名、公证、Gatekeeper 和干净设备启动。首个实现切片仅提供开发构建脚本，不属于正式分发。

## 已有验证与缺口

2026-10-24 的 Windows Host 回归及 [协议验证脚本](../Tools/VerifyMacHostProtocol.ps1) 已覆盖能力查询、结构化错误、中文路径、翻译载入／保存／重载和纯 JSON 标准输出。

同日 Apple Silicon macOS 26 的远程验证通过 SwiftUI Debug／Release 编译、ARM64 Host 自包含发布、开发 `.app` 校验与进程启动，以及 LLDB 入口断点。Host 的多行与显式空翻译往返、输入覆盖保护和首个活动剧本下载后载入均通过；轴机按预期报告不可用，不加载最低要求 macOS 27.0 的原生库。

这些记录不是当前构建的完整验收，也不代表所有下载分类已通过。macOS 15 实机、视频识别、Mac 桌面窗口与输入交互、正式签名分发仍未验收；需按上方要求在目标设备补齐。
