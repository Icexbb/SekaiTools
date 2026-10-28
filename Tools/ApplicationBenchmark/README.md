# 应用处理链验证

Windows 独立工具，使用真实 WPF Dispatcher、Emgu `ToBitmapSource`、Image 控件离屏渲染、应用队列 `SequentialTaskQueue` 的链接源码，以及 `ProgressStore.SaveToPath` 的实际原子 JSON 写入实现。输出全部隔离在指定目录，不写用户进度和历史。

```powershell
dotnet publish Tools/ApplicationBenchmark -c Release -o .codex-build/application/tool
dotnet .codex-build/application/tool/ApplicationBenchmark.dll .codex-build/application/default 16 16 0 E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 E:/ProjectSekai/test/aprilfool_2024_01.mp4
```

参数为输出目录、OpenCV 进程线程数、预读容量上限、取消比例（0 表示不中断）、视频列表。视频必须具备同目录同名剧本与翻译。每份视频生成完整状态、ASS 和报告，记录 WPF 预览次数、转换与离屏渲染时间、进度保存次数与耗时，以及整个进程截至该项的峰值内存。

设置取消比例为 `0.35`，工具会停止第一段，确认最终检查点已写入，再从磁盘反序列化检查点、创建新处理器恢复到结束。结束后必须与连续处理的完整状态和 ASS 哈希比较。多个视频由现有队列顺序推进，并检查最终队列已清空。结束状态可能是 `Completed` 或 `EndOfStream`，报告明确区分。已恢复的旧 FrameSet 通过 `ReplayExportableCallbacks` 导出，包含视频末尾的部分结果，避免只导出恢复之后的新事件。

此工具测量实际位图转换和离屏 Image 渲染，不打开应用窗口，不包含完整字幕页、时间轴控件、窗口合成器或交互。历史只保存最终状态，未调用用户历史目录及去重逻辑。不能把工具耗时作为完整桌面应用的准确耗时，也不能把队列进程的累计峰值当作每个视频独立峰值。

## 多轮交替比较

分别发布并冻结原版本和候选版本，避免后续构建覆盖它们。脚本每次启动独立进程，轮流交换参考与候选的先后顺序；两个版本使用相同线程、预读设置、视频组和模板资源。

```powershell
./Tools/ApplicationBenchmark/RunComparison.ps1 `
  -ReferenceDll .codex-build/application/reference/ApplicationBenchmark.dll `
  -CandidateDll .codex-build/application/candidate/ApplicationBenchmark.dll `
  -OutputDirectory .codex-build/application/comparison `
  -Videos E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4,E:/ProjectSekai/test/aprilfool_2024_01.mp4
```

默认三轮，4 线程、8 帧预读。结果包含各版本耗时中位数、每轮配对降幅的中位数，以及预览次数与累计转换渲染耗时。配对降幅与两个耗时中位数之比不是同一统计量。

脚本校验所有轮次的完整状态与 ASS 一致，并对实际文件重新计算哈希；报告存在时可继续验证，输入、DLL、运行配置与轮次数变化会拒绝复用。模板资源纳入输入指纹并在结束时复核；变更资源必须使用新输出目录。不同目录的同名视频不能混入同一次比较，避免输出覆盖。独立进程没有额外预热，测量包括初始化；测试期间应避免其他构建或性能测试负载。全部比较仍为离屏应用链，不能替代完整窗口测量。

为隔离恢复边界与原生后端，工具可读取 SEKAI_BENCHMARK_RESUME_STATE 指定的 JSON 检查点。将 cancelFraction 设为 0；该路径只能用于研究工具，运行后恢复环境变量。检查点会通过现有 ApplyState 验证视频、剧本、翻译及配置，不跳过兼容性检查。报告 ResumeFrame 记录实际重放起点，仍保存最终状态和 ASS；与连续处理完整结果比较，不能只看字幕。

```powershell
$env:SEKAI_BENCHMARK_RESUME_STATE = (Resolve-Path .codex-build/native/resumed-ipp/aprilfool_2024_01.mp4/canceled-state.json).Path
dotnet .codex-build/native/application-stock-replay/ApplicationBenchmark.dll .codex-build/native/replay-stock 4 8 0 E:/ProjectSekai/test/aprilfool_2024_01.mp4
Remove-Item Env:SEKAI_BENCHMARK_RESUME_STATE
```

指定外部检查点时只能提供对应的一份视频，不能同时进行比例取消。外部重放不含取消前的耗时，不能与整段取消／恢复时间直接比较。

## 精确帧边界取消

设置 `SEKAI_BENCHMARK_CANCEL_FRAME` 为正整数，取消比例设为 0，并仅传入一份视频。工具通过每帧的预览启用回调在处理线程读取当前已解码帧索引，调用取消后仍完成当前帧匹配；下一次循环停止。该入口只存在于研究工具，通过反射读取处理器索引，不修改产品接口。工具要求实际保存的取消帧恰好等于请求值，再从磁盘检查点创建新处理器恢复。报告同时记录 `CancelFrame` 和 `ResumeFrame`。

```powershell
$previousCancelFrame = $env:SEKAI_BENCHMARK_CANCEL_FRAME
try {
    $env:SEKAI_BENCHMARK_CANCEL_FRAME = '1029'
    dotnet .codex-build/boundary/application/ApplicationBenchmark.dll .codex-build/boundary/frame-1029 4 8 0 E:/ProjectSekai/test/aprilfool_2024_01.mp4
} finally { $env:SEKAI_BENCHMARK_CANCEL_FRAME = $previousCancelFrame }
```

不能与比例取消或外部检查点重放组合，超出实际处理范围的请求会因未发生取消而失败。反射与额外读索引仅用于正确性验证，不能用这些耗时作性能结论。连续比较脚本拒绝带此环境变量运行，避免把取消恢复混入普通性能比较。

## 分块队列检查点验证

设置 `SEKAI_BENCHMARK_CHUNKED=true` 后，工具通过隔离路径调用产品 `IncrementalProgressStore`，并声明增量快照能力。当前存储实现按紧凑 JSON 大小自适应选择全量或分块；该标记表示使用队列增量接口，不保证每次保存均产生分块。默认仍使用旧全量原子 JSON，便于同一工具对照。历史输出仍为完整 JSON。报告的 `ChunkedCheckpoint` 标记本次模式。

取消比例或精确帧入口均可与此模式组合。工具要求最终磁盘检查点恢复后的完整 JSON 与取消时公开捕获的状态完全一致，再创建新处理器恢复。最终的 `state.json` 和 `subtitle.ass` 必须与连续运行比较；单次通过仅证明对应素材和取消边界，不能外推全部视频或作为性能提升结论。外部检查点入口仍只接收已展开的完整 JSON。

```powershell
$previousChunked = $env:SEKAI_BENCHMARK_CHUNKED
try {
    $env:SEKAI_BENCHMARK_CHUNKED = 'true'
    dotnet .codex-build/recognition-plan/checkpoint/ApplicationBenchmark.dll .codex-build/recognition-plan/chunked-long/continuous 4 8 0 E:/ProjectSekai/test/event200_tsukasa_01.mp4
    dotnet .codex-build/recognition-plan/checkpoint/ApplicationBenchmark.dll .codex-build/recognition-plan/chunked-long/resumed 4 8 0.35 E:/ProjectSekai/test/event200_tsukasa_01.mp4
} finally { $env:SEKAI_BENCHMARK_CHUNKED = $previousChunked }
```

`RunComparison.ps1` 拒绝环境变量隐式启用分块模式；比较时通过 `-ReferenceCheckpointMode legacy|chunked` 和 `-CandidateCheckpointMode legacy|chunked` 显式选择，默认两边均为 legacy。模式、模板摘要和输入及二进制摘要进入身份清单，运行结束再次核对身份。模式变化必须使用新输出目录；分块模式要求报告确实声明该能力，旧工具不能冒充支持分块。取消恢复仍通过单独入口验证。

`VerifyChunkedResume.ps1` 将新连续运行与多个精确取消边界顺序对照，默认使用 4095、4096、4097 帧。它要求新的输出目录，记录输入三件套和工具目录内 DLL/JSON 的 SHA256，运行结束再次核对身份；每次运行必须声明分块模式，实际取消和恢复帧必须等于请求值，最终状态和 ASS 文件长度及摘要均必须一致。只有全部通过才生成 `verification.json`。运行时临时设置环境变量，结束或失败均恢复原值。

```powershell
./Tools/ApplicationBenchmark/VerifyChunkedResume.ps1 `
  -BenchmarkDll .codex-build/recognition-plan/checkpoint/ApplicationBenchmark.dll `
  -Video E:/ProjectSekai/test/event200_tsukasa_01.mp4 `
  -OutputDirectory .codex-build/recognition-plan/chunked-boundaries
```

自定义边界使用 `-CancelFrames 1029,8192`；边界超出实际识别范围会失败。脚本固定同一构建、配置和素材，仅用于恢复正确性，不进行版本性能比较，也不能据此声称所有视频或取消时刻均等价。运行期间不要构建或更改模板资源。

## 同构建检查点模式比较

参考和候选可以指向同一个固定构建，将旧全量与当前队列存储策略作为唯一变量。三轮、独立进程、交替先后顺序，并启用真实 WPF 离屏预览；每轮核对完整状态与 ASS 的实际文件摘要。除总处理时间外，汇总保存调用累计耗时与保存次数中位数。保存耗时只统计持久化接口内的序列化和写盘，不包含处理线程上的快照构造。保存是后台异步的，保存耗时不能直接从总耗时扣除；合并待写快照会影响调用次数，两者应结合报告判断。

```powershell
./Tools/ApplicationBenchmark/RunComparison.ps1 `
  -ReferenceDll .codex-build/recognition-plan/checkpoint/ApplicationBenchmark.dll `
  -CandidateDll .codex-build/recognition-plan/checkpoint/ApplicationBenchmark.dll `
  -OutputDirectory .codex-build/recognition-plan/checkpoint-modes-comparison `
  -Videos E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4,E:/ProjectSekai/test/event200_tsukasa_01.mp4 `
  -ReferenceCheckpointMode legacy -CandidateCheckpointMode chunked
```

只有整组运行及结束身份核对通过后才生成 `summary.json`，中断时保留逐轮报告用于身份一致的续跑。模板资源也纳入身份核对；这些保证不能替代控制外部系统负载和温度。该比较仅隔离检查点模式，不代表全部识别算法改进的累计收益。
