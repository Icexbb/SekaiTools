# 同步灰度断点恢复资格工具

隔离研究工具，调用实际 ProgressStore.SaveToPath 原子 JSON 写入器；输出目录必须全新，进度和历史只写该目录。需要带灰度捕获工厂的固定原生运行库与已核验的 FFmpeg 插件。已在三轮性能复测结束后编译并执行，未与性能测量并行运行。

调用：`GrayResumeProbe <video> <qualified-plugin> <new-output-directory> <continuous|cancel|resume> <frame|checkpoint|-> [matchingThreads=4] [buffer=8]`。cancel 在处理线程精确帧回调取消，确认该帧处理完毕后的捕获状态与实际保存检查点完整一致。resume 从另一个新进程读取检查点，严格核对输入和运行库身份后，仅在内存还原实验身份，再调用原有 ApplyState。两种模式均不改变产品处理器、匹配算法或用户目录。

所有落盘状态及历史含 BenchmarkOnlyGrayCapture 身份，包含实际原生库、插件与解码线程；确认原产品配置拒绝恢复。结果包含完整状态与 ASS 文件哈希、实际检查点路径／哈希及持久化执行证明，比较器逐字段核对连续与恢复后的状态，并比较实际 ASS 字节。该工具关闭预览，不用于性能结论或 GUI 资格证明。

april 的 1028、1029、1030 帧各执行一次精确取消和一次新进程恢复，共六次独立运行，全部通过实际原子检查点／历史与捕获状态对比，恢复后的完整状态、ASS 与连续灰度识别一致，指针已释放，输入／模板／工具清单前后未变。1029 帧的 Dialog 和 Banner 均有 PreviousSkippedFrameIndex=1029，实际覆盖了待回查帧重建。证据为 `.codex-build/gray-resume/april-boundaries/summary.json`。这仅证明该素材三个边界，不证明任意断点、旧版检查点或彩色预览。
