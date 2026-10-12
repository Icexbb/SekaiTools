# 标准视频识别性能对比

本工具使用本机已有模板资源，读取指定视频及同目录、同名 `.json` 剧本和 `.txt` 翻译。
不下载资源，不写入应用的进度和历史目录。输出目录必须明确指定。

```powershell
dotnet run --project Tools/RecognitionBenchmark -c Release -- "E:/ProjectSekai/test/aprilfool_2024_01.mp4" ".codex-build/benchmark/current" 3 false Automatic false
```

参数依次为视频、输出目录、正式运行次数（默认 3）、是否生成预览（默认 false）、性能模式（默认 Automatic）、是否提前灰度化（默认 false）、OpenCV 线程数（省略时保持运行库默认）、是否开启详细计时（工具默认 true）、是否预热（默认 true）、预读容量上限（省略时采用模式默认值）、是否使用内容位置探测（默认 false）、是否只解码（默认 false）、是否实验性硬件解码（默认 false）。
线程数只用于这个独立进程的实验，不修改应用的全局配置。

产品默认 `RecognitionPerformanceOptions.CollectDetailedMetrics=false`，跳过匹配算子的细分计时。
诊断热点时开启详细计时；评价正式运行速度时关闭，以免把采样开销当作算法耗时。
例如在本机默认 16 线程下关闭详细计时：

```powershell
dotnet run --project Tools/RecognitionBenchmark -c Release -- "E:/ProjectSekai/test/aprilfool_2024_01.mp4" ".codex-build/benchmark/current" 3 false Automatic false 16 false
```

默认每次启动先预热一次，再正式运行指定次数。每轮创建新的处理器，输出完整处理状态、ASS 字幕和 JSON 性能报告。
比较前后版本时使用相同 OpenCV 版本、素材、资源、设置和硬件，并串行运行，避免两个识别任务互相争用 CPU。

使用正式轮次的 `Performance.ElapsedTime` 中位数比较吞吐量；`WallMilliseconds` 还包含结果序列化和字幕导出。
`PeakWorkingSet` 是整个基准进程截至当前轮次的峰值，包含预热和前面轮次，不表示单轮独立峰值。
解码和预处理可以与匹配重叠，因此算子时间之和不能用于计算总耗时或加速比。

`Matching` 提供各用途的请求数、缓存命中、总耗时，以及 ROI 缩放、相关计算、分数清理和极值查找耗时。
这些细分计时包含于处理阶段计时中，不能再次相加。`ReadWaitTime` 测量有缓冲模式下消费者读取一帧的时间；`PreviewTime` 仅测量预览生成与入队，不含 UI 渲染。

比较所有正式轮次的 `StateHash` 和 `SubtitleHash`。状态包含起止帧、坐标、时间码、匹配器状态及素材元数据。
哈希不同必须检查输出差异，不能仅凭速度接受改动。使用未带细分计时 API 的旧版 Core 时 `Matching` 为 null。

```powershell
./Tools/RecognitionBenchmark/Compare.ps1 -Baseline .codex-build/benchmark/baseline -Candidate .codex-build/benchmark/current
```

比较脚本要求每组至少三次正式运行，输出耗时中位数及降低比例；状态或字幕哈希不一致时失败。

## 多素材实验

`RunSuite.ps1` 递归发现视频，以同目录、同名 `.json` 与 `.txt` 为完整识别组。缺少素材的组只参与 `decode`。阶段包括 `screen`/`verify`（原路径与位置探测逐轮核对完整状态和 ASS）、`threads`（4/8/16）、`buffers`（0/4/8/16）、`preview`（关闭/开启）、`decode`（纯解码，报告后端与实际帧数）。

```powershell
./Tools/RecognitionBenchmark/RunSuite.ps1 -InputDirectory E:/ProjectSekai/test -BenchmarkDll .codex-build/next/experiment/RecognitionBenchmark.dll -ReferenceDll .codex-build/next/current/RecognitionBenchmark.dll -OutputDirectory .codex-build/next/screen -Stage screen
```

使用 `-Runs 3` 测量预热后的三轮；`-VideoNames aprilfool_2024_01,event200_tsukasa_01` 可筛选代表素材。一次只运行一个阶段。输出目录中的已完成结果用于续跑；更换二进制、素材或设置后必须使用新目录。线程、容量、预览实验可传 `-ReferenceResultsDirectory .codex-build/next/screen`，逐轮与对应参考组核对完整状态及字幕哈希。

预读容量 0 禁用预读，其他值仍受模式上限和可用内存预算约束。纯解码读至 EOF，可能比识别完成位置更晚；直接比较时需考虑帧数差异。预览实验包含核心预览生成和队列成本，不包含 WPF 渲染。

使用 `SummarizeSuite.ps1` 汇总每种配置的正式轮次、中位数、范围与进程峰值内存，并核对全部正式轮次：

```powershell
./Tools/RecognitionBenchmark/SummarizeSuite.ps1 -ResultsDirectory .codex-build/next/threads -ReferenceResultsDirectory .codex-build/next/screen | Format-Table
```

纯解码汇总省略参考目录，检查实际解码帧数与声明帧数一致；不把一次测量当作稳定收益证据。

## 交替组合测试

`RunInterleaved.ps1` 对全部完整素材组比较 16 线程／16 帧、4 线程／16 帧、4 线程／8 帧。每份视频至少三轮，每轮轮换配置顺序，每次测量为独立进程且不额外预热，逐轮核对完整状态和 ASS。输入文件及工具、Core 二进制哈希写入清单，恢复运行时变化会报错，须改用新目录。

```powershell
./Tools/RecognitionBenchmark/RunInterleaved.ps1 -InputDirectory E:/ProjectSekai/test -BenchmarkDll .codex-build/next/final/RecognitionBenchmark.dll -OutputDirectory .codex-build/combined -Rounds 3
```

与配置连续运行多轮的旧实验不同，该布局在各配置下保存 `round-1/run-1.json` 等报告；汇总时按视频与配置聚合正式轮次。应用链补测见 `Tools/ApplicationBenchmark/README.md`。

```powershell
./Tools/RecognitionBenchmark/SummarizeInterleaved.ps1 -ResultsDirectory .codex-build/combined | Format-Table
```

## 硬件解码筛选

最后一个参数开启 FFMPEG `HwAcceleration.Any` 实验，报告实际 `DecodeAcceleration`。识别模式通过工具内的私有捕获对象替换进行实验，产品没有硬件解码选项。输出的硬件状态额外标记配置指纹，并实际验证原产品配置会拒绝恢复它；不得用实验输出代替用户检查点。

完整状态包含解码实验配置指纹，因此软硬件的 `StateHash` 预期不同。筛选使用 `RecognitionHash`（帧、时间码、匹配器状态）、`SubtitleHash`，并另外核对 `TemplateScales`。不通过通用 `Compare.ps1` 比较此实验的完整状态哈希。像素不同的候选只有在实际任务结果一致时才有继续评估价值。

```powershell
dotnet run --project Tools/RecognitionBenchmark -c Release -- E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/hardware/reproduce 1 false Automatic false 4 false false 8 false false true
```

改用最后参数 `false` 并指定另一个目录建立软件参考。本机五组筛选中三组字幕改变，硬件路径未获接受；单轮筛选不足以宣称任何稳定提速。

## 独立解码线程实验

匹配线程和帧缓冲位置参数可填 `default`：前者不设置 OpenCV 线程数，后者保留自动缓冲，适合在后续位置仍需提供参数时复现产品默认选择。实际线程数由报告记录，实际自动容量需从启动日志核对，不能假设所有机器均为相同值。

第十四个位置参数为可选解码线程数 1～64。工具通过原有反射钩子将处理器捕获器替换为指定 CAP_PROP_N_THREADS=70 的软件 FFmpeg 捕获器，核对实际线程数等于请求值。不能与硬件解码或只解码模式组合；只解码的线程检查使用 DecoderProbe。产品没有新增该设置，输入和模板均只读，进度及历史仍不写用户目录。

```powershell
dotnet .codex-build/decoder-recognition/tool/RecognitionBenchmark.dll E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/decoder-recognition/short-4 1 false Automatic false 4 false false 8 false false false 4
```

报告记录 RequestedDecoderThreads、ActualDecoderThreads、OriginalConfigHash 和 StateHashBeforeExperimentTag。线程／硬件实验的导出状态加入实验配置身份，运行时确认产品原配置拒绝恢复。比较完整状态时，仅可还原此已知身份字段，并逐字段核对其余全部数据；同时核对标记前完整状态哈希及实际 ASS、标记后状态文件哈希，不能以 RecognitionHash 替代完整状态检查。单轮只用于资格试验，不作稳定性能结论。

## 完整解码对照和原生运行库复测审计

`VerifyDecoderPair.py` 比较未指定解码线程的原参考与显式线程候选，核对文件实物哈希、软件 FFmpeg、实验身份生成公式、完整状态、ASS 字节、终止原因和计时帧数；不会修改状态文件。不允许 Python -O 关闭断言。

```powershell
python Tools/RecognitionBenchmark/VerifyDecoderPair.py .codex-build/decoder-recognition/short-default .codex-build/decoder-recognition/short-4
python Tools/RecognitionBenchmark/SummarizeNativePair.py .codex-build/decoder-recognition/ipp16-repeat
```

第二个工具仅用于固定五组、三轮的 stock／ipp16 布局：`素材/round-N/stock` 与 `素材/round-N/ipp16`，需要根目录 manifest-before.json 的全部文件指纹。要求匹配 4 线程、预读 8、实际解码 16 线程，无预览／预热／详细计时，拒绝缺轮次、不同工作量或跨轮输出变化。输出配对降幅、中位耗时、范围及峰值内存；完整输出审计通过不等于性能有收益，也不等于原生库可以发布。

## 原生同步灰度捕获实验

`RunDefaultGrayScreen.ps1` 顺序运行五组原包／灰度对照，使用 `default` 匹配线程与自动缓冲，验证双方实际运行库身份、完整状态、ASS 及输入／模板／工具清单；从启动日志提取实际自动缓冲容量。首次筛选证据为 `.codex-build/gray-defaults/qualification/summary.json`：五组均等价，本机实际 OpenCV 16 线程、自动缓冲 16 帧；short、april、event152、event200、wl 单次耗时降幅分别为 3.28%、11.81%、12.81%、14.45%、5.83%。这仍是无预览的单轮筛选，不能替代默认配置重复测量或 GUI 彩色预览验证。

第十五个位置参数可指定已核验的 FFmpeg 插件路径，启用隔离 cvextern 内的灰度捕获工厂。必须指定解码线程 16，且预览、PrepareGrayFrames、硬件解码、只解码均为 false。内部指针包装与插件库生命周期由研究工具持有，处理器先释放，再卸载插件库；产品捕获接口与识别算法保持原状。

```powershell
dotnet .codex-build/gray-recognition/tool-ipp/RecognitionBenchmark.dll E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/gray-recognition/new-short-gray 1 false Automatic false 4 false false 8 false false false 16 .codex-build/gray-recognition/tool-ipp/runtimes/win-x64/native/opencv_videoio_ffmpeg500_64.dll
python Tools/RecognitionBenchmark/VerifyGrayPair.py .codex-build/gray-recognition/short-reference .codex-build/gray-recognition/short-gray
```

报告含 NativeGrayCapture、实际加载原生库及插件哈希。状态采用独立 BenchmarkOnlyGrayCapture 实验身份，包含后端、加速类型、解码线程及两个库哈希，产品原配置必须拒绝恢复。VerifyGrayPair 只还原已核验的这一个身份字段，逐字段比较其余完整状态，并核对实际 ASS 字节、实物文件哈希、终止原因与计时帧数。比较器已用修改状态、身份、ASS、帧数、终止原因五种负例验证，连同重新计算报告文件哈希的状态／ASS 负例均被拒绝。一次完整对比仅为资格检查，不证明稳定性能收益或 GUI 预览、断点恢复资格。

五组首次完整对比全部等价，且普通路径状态／ASS 与此前原包输出逐字节一致；同一实验运行库内普通捕获与灰度捕获的单次耗时分别为 short 7.20／6.67 秒、april 41.40／39.78 秒、event152 40.38／35.95 秒、event200 57.45／49.23 秒、wl 31.36／29.44 秒。输入、模板和工具清单复核未变（清单在 short 两次先行检查之后、其余八次执行之前建立），证据为 `.codex-build/gray-recognition/qualification-summary.json`。这些跨进程单次结果仅提供复测依据。

增强报告在计时开始前记录实际加载的 NativeLibraries，`VerifyGrayPair.py --require-native-identity` 要求双方都有记录，并核对实际库文件哈希与灰度捕获身份一致。旧资格报告没有双方库列表时，工具会明确返回 ActualNativeIdentitiesRecorded=false；它们不能用于严格复测汇总。增强工具已在原包／灰度短视频对照通过完整输出及双方实际运行库身份检查。

`RunGrayComparison.ps1` 使用同一托管 runner 的原包与隔离灰度运行库，顺序执行固定五组、三轮、共三十个新进程，第二轮交换先后顺序。`SummarizeGrayComparison.py` 复核输入、模板、工具和审计脚本清单，严格验证三十份实际输出及运行库身份，并检查跨轮参考状态／字幕未变；分别报告配对降幅中位数、降幅范围、两组耗时中位数与峰值内存。这一复测衡量 IPP 运行库加灰度路径的组合收益，不能全部归因于 OpenCV 5 新增 HAL 或同步回调本身。

默认配置三轮复测已完成，证据 `.codex-build/gray-defaults/three-round/summary.json`。`RunDefaultGrayComparison.ps1` 交替顺序执行三十个独立进程，`SummarizeDefaultGrayComparison.py` 逐配对核对完整状态、ASS、实际库身份和实际自动缓冲容量，并检查跨轮输出及输入／模板／工具清单未变。本机全部使用 OpenCV 16 线程、自动 16 帧缓冲。配对耗时降幅中位数：short 12.52%、april 11.47%、event152 13.71%、event200 15.70%、wl -3.36%。各组范围依次为 11.11～13.10%、10.74～13.91%、13.47～13.87%、-8.33～17.39%、-37.85～6.60%。所有结果保留，不能剔除回退轮次或据此前单轮筛选声称普遍加速；当前不足以将灰度路径设为产品默认。GUI 彩色预览与该配置下的恢复仍未取得资格，报告中的 ResumeQualified=false 不否定此前独立 4 线程恢复试验。

第十六个位置参数 `true` 改用周期彩色捕获工厂，第十五个参数仍为已核验插件路径。该实验允许开启预览，但仍要求 PrepareGrayFrames、硬件解码、只解码为 false，解码线程 16。工厂间隔按视频 fps 与产品公式计算，独立 `BenchmarkOnlyPeriodicColorCapture` 状态身份包含间隔和两个实际库哈希；不得与旧灰度身份混用。报告新增 NativePeriodicColorCapture、周期及库身份、PreviewFrames、NonColorPreviewFrames；开启预览时必须实际收到回调且全部为三通道，失败则整个资格运行失败。

`VerifyPeriodicColorPair.py --require-native-identity` 核验完整状态及 ASS，只还原已验证的周期彩色身份字段，核对双方预览选择和实际原生库。`CheckPeriodicVerifier.py` 已验证六种负例被拒绝：重算实物哈希的状态／ASS 改动、缺失预览回调、非三通道预览、错误周期、运行库身份改动。短视频开启预览的完整状态／ASS 相同，双方分别收到 40／41 次三通道回调，证据 `.codex-build/periodic-recognition/short-reference` 和 `short-candidate`；单次耗时 8.58／8.97 秒不证明性能收益，回调资格也不证明实际 WPF 显示。

`RunPeriodicColorScreen.ps1` 已完成默认线程／自动缓冲下五组开启预览的顺序资格对照；证据 `.codex-build/periodic-recognition/five-preview/summary.json`。全部完整状态、ASS、实际库身份和三通道预览回调一致，输入／模板／工具清单未变。本机实际 16 线程、16 帧自动缓冲；short、april、event152、event200、wl 单次耗时降幅依次 1.04%、4.67%、0.24%、8.69%、2.00%。此为单轮回调资格，不证明稳定收益或实际 WPF 预览，也不能取代周期彩色断点恢复验证。

开启彩色预览的三轮复测已完成，证据 `.codex-build/periodic-recognition/three-round-preview/summary.json`。RunPeriodicColorComparison.ps1 顺序执行三十个新进程，第二轮反转顺序，SummarizePeriodicColorComparison.py 要求周期彩色身份、预览开启、双方实际库身份、自动缓冲一致，逐配对核验完整状态／ASS、跨轮结果及输入／模板／工具清单未变。本机全部 16 线程、自动 16 帧。short、april、event152、event200、wl 配对耗时降幅中位数分别 5.99%、7.04%、3.90%、4.49%、1.54%，范围分别 4.38～6.96%、-6.25～9.36%、2.09～5.24%、3.56～5.62%、-1.78～4.19%；两次回退均保留。每对峰值工作集差值约 68～96 MiB。此结果不包含 GUI 的 ToBitmapSource／Dispatcher 显示开销，不能当作整个 GUI 加速比例；独立恢复验证另见 PeriodicColorResumeProbe。
