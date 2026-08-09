# SekaiTools 架构与项目边界

## 目标

解决方案按“稳定领域模型、可复用处理能力、外部基础设施、桌面组合根”划分。项目边界用于限制依赖方向，而不是按文件类型机械拆分。

## 项目职责

| 项目 | 职责 | 不应包含 |
|---|---|---|
| `SekaiToolsBase` | 游戏数据 DTO、剧本解析、翻译合并、共享日志与代理配置 | OpenCV、WPF、资源下载、处理进度存储 |
| `SekaiToolsConfiguration` | 统一加载、校验并发布网络端点配置 | 业务模型、网络请求、UI 状态 |
| `SekaiToolsSubtitles` | ASS 文档、样式、事件、标签与绘图模型 | 游戏数据、视频识别、文件下载 |
| `SekaiToolsCore` | 视频读取、模板匹配、FrameSet、字幕生成、处理状态模型与基础设施契约 | HTTP 下载、用户目录、JSON 文件存储、WPF |
| `SekaiToolsMedia` | VSPipe/FFmpeg 压制管线、编码参数和进度模型 | WPF ViewModel、Snackbar、Windows 电源管理 |
| `SekaiDataFetch` | 远程游戏数据获取、数据源选择、缓存写入 | 页面控件、导航和提示框 |
| `SekaiToolsInfrastructure` | 模板资源下载校验、进度与历史文件存储，以及 Core/Media 契约实现 | 页面状态和业务交互 |
| `SekaiToolsGUI` | WPF 视图、ViewModel、导航、交互编排和 Windows 集成 | 可复用的视频算法、HTTP 实现、FFmpeg 管线 |
| `Updater` | 独立更新进程 | 除统一配置外的主程序程序集依赖 |
| `SekaiTools.Tests` | 非 UI 项目的单元测试与兼容性测试 | 产品运行时代码 |

`SekaiToolsSubtitles` 暂时保留 `SekaiToolsBase.SubStationAlpha` 公共命名空间，以维持源码与序列化兼容；程序集边界已经独立，命名空间迁移应另行作为破坏性变更处理。

## 允许的依赖方向

```text
SekaiToolsConfiguration     SekaiToolsBase            SekaiToolsSubtitles       SekaiToolsMedia
      ↑                              ↑                       ↑
      ├──────── SekaiDataFetch       │                       │
      └──────── SekaiToolsCore ──────┘                       │
                       ↑                                    │
                       └──── SekaiToolsInfrastructure ───────┘
                                      ↑
                                      │
                    SekaiToolsGUI ─────┴──→ SekaiDataFetch/Core/Media/Subtitles

Updater → SekaiToolsConfiguration（独立进程，无其他编译期入边）
SekaiTools.Tests → 所有被测非 UI 项目
```

必须遵守以下规则：

1. `Core` 和 `Media` 不引用 `Infrastructure` 或 `GUI`；外部能力通过各自定义的接口传入。
2. `Infrastructure` 实现下层项目定义的契约，不持有页面或 ViewModel 状态。
3. `GUI` 是组合根，负责把 `ResourceManager`、`ProcessingStatePersistence` 等实现注入处理服务。
4. `Updater` 由 GUI 的发布目标构建和复制，仅引用 `SekaiToolsConfiguration`。
5. 项目若直接使用另一个程序集的公共类型，应声明直接 `ProjectReference`，不依赖偶然的传递引用。

## 新代码放置规则

- 游戏 JSON、Story 或翻译领域规则：`SekaiToolsBase`
- 网络服务地址及默认数据源端点：根目录 `network-endpoints.json`，由 `SekaiToolsConfiguration` 负责加载与校验
- ASS 格式及渲染字符串模型：`SekaiToolsSubtitles`
- 视频识别与字幕生成算法：`SekaiToolsCore`
- FFmpeg/VapourSynth 流程：`SekaiToolsMedia`
- HTTP 游戏数据访问：`SekaiDataFetch`
- 文件系统、资源服务器、进度历史实现：`SekaiToolsInfrastructure`
- WPF 控件、ViewModel、通知、电源请求：`SekaiToolsGUI`

当一个功能同时涉及 UI 和业务流程时，先在非 UI 项目中设计参数、结果和进度模型，再由 GUI 做绑定与交互编排。
