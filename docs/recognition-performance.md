# 识别性能与诊断

本文保留识别优化的运行约束、最终取舍与复测入口；不将历史实验的单次输出或未完成候选包当作当前发布状态。识别算法及未完成校准见 [字幕识别规则](recognition.md)。

## 缓存与预读

逐帧上下文共享灰度图，按 ROI 延迟生成缩放层；匹配模板及 Gray/Alpha 多尺度层按任务复用。结果缓存只复用同一帧、同一图像、ROI、模板及匹配算法的结果，不跨任务共享，也不做跨帧逐像素比较和整图克隆缓存。相关实现见 [FrameMatchContext](../SekaiToolsCore/Match/TemplateMatcher/FrameMatchContext.cs) 和 [TemplateMatchCachePool](../SekaiToolsCore/Match/TemplateMatcher/TemplateMatchCachePool.cs)。

性能模式由 [RecognitionPerformanceOptions](../SekaiToolsCore/Process/Config/RecognitionPerformanceOptions.cs) 定义：

| 模式 | 新增缓存预算上限 | 帧池上限 |
| --- | ---: | ---: |
| 节省内存 | 128 MiB | 4 帧 |
| 自动（默认） | 512 MiB | 16 帧 |
| 高性能 | 用户设置 128–4096 MiB，默认 2048 MiB | 32 帧 |

启动时再按可用物理内存的四分之一限制预算，节省内存预留 64 MiB，其他模式预留 96 MiB 给匹配及预生成缓冲。BGR 帧按每像素 3 字节估算，开启后台灰度后按 4 字节；不足两帧则回退串行读取。用户设置的预读上限只能进一步限制容量，预算不是整个进程的内存上限，也不会预先全部分配。

解码线程独占捕获器，顺序交付帧号、实际时间码及帧对象，池满则等待、不丢帧。检查点记录已消费位置，不跳过预读但未识别的帧。停止、正常结束及异常均先等待解码任务，再释放帧池和捕获器。

## 模板、预览与资源生命周期

识别缓存紧凑的匹配模板，不因匹配而保留大幅原彩色文字图；裁剪结果拥有独立存储，Skia 到 OpenCV 避免中间托管大数组。公开模板缓存与调用方拥有的临时图像区分释放责任。

自动／高性能模式预生成后续各最多四个对话、横幅和角标模板及缩放层，缓存上限 16 MiB，预算另预留临时画布空间；节省内存模式不启用。重置先取消并等待预生成任务，再释放缓存。

预览保持比例缩至 960×540 内，不放大小视频；关闭预览时跳过缩放和排队。结果 UI 使用有界分批队列并合并时间轴重绘。识别仍使用完整帧，预览尺寸不是匹配分辨率。后台灰度及内容位置探测默认关闭，详细匹配计时也默认关闭。

重置等待工作线程、结束回调及后台进度保存，释放处理器后再清空预览、列表和时间线。用户明确重置时可执行后台完整回收，不在逐帧循环中触发，也不强制裁剪工作集。原生运行库和 WPF 热身会提高常驻占用，不能用冷启动内存作为释放目标；应观察热身后的连续任务、弱引用和解码器句柄。

## 基准与正确性对照

工具各自的参数与复现命令维护在对应 README：

| 工具 | 用途 |
| --- | --- |
| [RecognitionBenchmark](../Tools/RecognitionBenchmark/README.md) | 完整识别状态、ASS、线程／缓冲实验及交替汇总 |
| [ApplicationBenchmark](../Tools/ApplicationBenchmark/README.md) | WPF 预览与实际进度写入的应用链对照 |
| [DecoderProbe](../Tools/DecoderProbe/README.md) / [DecodeStageProbe](../Tools/DecodeStageProbe/README.md) | 解码像素、时间码、定位及 Grab/Retrieve 成本 |
| [OpenCvCapabilities](../Tools/OpenCvCapabilities/README.md) | 实际加载运行库、构建能力及线程信息 |
| [NativeRuntimeExperiment](../Tools/NativeRuntimeExperiment/README.md) | 固定源码、依赖、模块及原生候选身份审计 |
| [MaskedMatchingProbe](../Tools/MaskedMatchingProbe/README.md) | 掩码模板预处理的独立算子实验 |
| [PeriodicColorCaptureProbe](../Tools/PeriodicColorCaptureProbe/README.md) / [PeriodicColorResumeProbe](../Tools/PeriodicColorResumeProbe/README.md) | 周期彩色通道、资源释放与断点恢复资格 |
| [PeriodicColorWpfProbe](../Tools/PeriodicColorWpfProbe/README.md) / [PublishedLayoutProbe](../Tools/PublishedLayoutProbe/README.md) | 队列、窗口及发布目录加载行为 |

正式比较固定输入三件套、模板、运行库、配置及硬件；至少三轮、独立进程交替执行，期间不并行构建或运行其他性能作业。诊断时开启细分计时，测量最终速度时关闭采样开销。

逐轮核对完整状态、坐标、时间码、尺度、终止原因及实际 ASS 字节。实验身份只能按事先明确的规则验证，不能任意删除差异字段。取消恢复要用磁盘检查点及新处理器与连续识别比较，不能仅测普通完成路径。

区分处理阶段耗时、整进程墙钟时间和 WPF 渲染；预读使阶段重叠，耗时不能直接相加。峰值工作集需说明是否包含预热和前轮。单轮用于筛选，前缀像素检查只能发现早期差异，不能证明完整视频等价。

## 历史实验的最终取舍

下列结论来自 2026-10 的本机样本，保留作复测依据，不是本次文档整理重新测得的结果。

| 方向 | 证据与取舍 |
| --- | --- |
| 紧凑模板缓存 | 80 个文本的局部测试，私有内存增量从约 629 MiB 降至 6 MiB；不代表整任务按同一比例减少。 |
| 有界预读 | 1080p/600 帧空剧本合成样例吞吐改善，但增加帧池内存；不能外推到完整剧情。 |
| 后台灰度 | 合成样例未显示稳定提速，峰值内存增加，保持默认关闭。 |
| 低匹配线程／较小帧池 | 五组素材、三轮交替的完整状态与 ASS 一致，4 线程／8 帧组合在该机降低耗时与内存；设置仍默认自动，不推导通用最优值。 |
| 实际时间预览节流 | 两份素材的预览转换和渲染累计成本显著下降，长片总耗时降低，短片有回退；不宣称所有素材更快。 |
| MSMF 替换 FFmpeg | 八份前缀检查存在打不开、像素或时间码差异，未接受为默认后端。 |
| D3D11 硬件解码 | 五组完整识别中三组状态及 ASS 改变，且单轮均更慢；撤回产品分支，只保留隔离实验。 |
| 浮点模板／掩码缓存 | 输出一致但完整任务收益混合、内存增加，未进入产品；复现补丁留在工具目录。 |
| AVX2、IPP 及低线程组合 | 必须核对实际原生构建；不能将已有算子分派或组合收益全部归因于 OpenCV 5 新特性。 |
| BGRA 输出 | 前缀像素检查通过，但代表样例更慢且增加帧存储，未采用。 |

周期彩色是默认关闭的独立实验：识别帧输出灰度，按预览周期保留彩色；[产品工厂](../SekaiToolsInfrastructure/Video/PeriodicColorCaptureFactory.cs) 校验运行库及插件指纹，并把实际身份写入检查点，禁止不同身份混用。

完整模块扩展候选在当时的五组、三轮共十五对中通过状态与 ASS 对照，整进程配对耗时降幅中位数约 1.00%–5.79%，峰值工作集下降约 68–97 MiB，但有一对耗时回退；最后两对为中断后补测，不是连续测量。有限模块旧候选不等同完整 SDK，模块／导出数一致也不能证明完整 ABI。窗口行为探针与可见界面人工验收分开，历史本地候选目录、版本及哈希不得当作当前正式发布依据。

## 文件选择延迟排查

浏览与拖放最终都进入 [SubtitleTaskSetup](../SekaiToolsGUI/View/Subtitle/Components/SubtitleTaskSetup.xaml.cs) 的配套文件选择逻辑；浏览额外调用 Windows 文件对话框。配套查询在后台执行，关闭自动匹配则跳过，连续选择或关闭窗口会使旧结果失效。

日志区分对话框开始、`FileOk`、返回及配套查询。对话框总耗时包含用户操作，`FileOk` 也不是鼠标点击时刻，它发生在路径获取和校验之后。`FileOk` 到返回很长时先查关闭阶段；确认后到 `FileOk` 很长时查系统路径验证，不能只凭总时间归因于缩略图、杀毒软件或最近文档登记。

周期彩色资格检查在启动时触发并在进程内缓存，普通非实验任务跳过实验工厂；实际使用和恢复实验状态仍校验身份。2026-10-12 的本机配套匹配测试未复现长阻塞，不能称用户问题已彻底解决。若仍可复现，应在该设备采样实际对话框调用栈和分段日志；工具入口见 [PeriodicColorSettingsProbe](../Tools/PeriodicColorSettingsProbe/README.md)。
