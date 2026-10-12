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

脚本校验所有轮次的完整状态与 ASS 一致，并对实际文件重新计算哈希；报告存在时可继续验证，输入、DLL、运行配置与轮次数变化会拒绝复用。模板资源需保持不变；它们不在本脚本的输入指纹中，变更资源必须使用新输出目录。不同目录的同名视频不能混入同一次比较，避免输出覆盖。独立进程没有额外预热，测量包括初始化；测试期间应避免其他构建或性能测试负载。全部比较仍为离屏应用链，不能替代完整窗口测量。

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
