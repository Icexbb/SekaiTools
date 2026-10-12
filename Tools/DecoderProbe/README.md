# 解码后端前置检查

```powershell
dotnet run --project Tools/DecoderProbe -c Release -- E:/ProjectSekai/test .codex-build/decoder/probe.json
```

递归只读全部视频，对 FFMPEG 和 MSMF 前 64 帧逐帧比较图像尺寸、通道数、像素最大差值、帧索引和时间码。后端打不开或输出不同则记录拒绝原因；不修改产品后端。报告中的时间包含两个解码器和像素校验，不是后端性能对比。

即使前缀一致，也必须完成全视频、随机定位、检查点恢复与字幕结果验证，再做独立重复性能测量；本检查不能证明替换后端安全或更快。

第三个参数可选 `Msmf`（默认）、`FfmpegAny` 或 `FfmpegD3D11`。后两项在打开 FFMPEG 时传入 `HwAcceleration`，报告请求与实际加速类型，避免把软件回退当作硬件解码。

```powershell
dotnet run --project Tools/DecoderProbe -c Release -- E:/ProjectSekai/test .codex-build/hardware/any.json FfmpegAny
```

像素不同需要记录并分析，不等于字幕必然不同。当前硬件实验另用独立识别基准检查实际任务输出；产品仍使用原解码路径。

## FFmpeg 解码线程检查

第三个参数支持 `FfmpegThreadsN`，N 为 1～64；打开候选捕获器时传入 OpenCV `CAP_PROP_N_THREADS`（数值 70，open-only）。Emgu 当前枚举没有该名称，数值转换仅放在研究工具中，不修改产品解码器或全局环境。报告记录 ReferenceThreads、RequestedThreads、ActualThreads 和 ThreadRequestApplied，实际线程数不等于请求值时拒绝候选。

第四个参数为顺序读取帧数上限（默认 64，0 表示全部）。报告时间包含两份解码和像素校验，不能解释为线程设置的性能收益。

```powershell
dotnet .codex-build/decoder-threads/probe/DecoderProbe.dll E:/ProjectSekai/test .codex-build/decoder-threads/prefix-4.json FfmpegThreads4
dotnet .codex-build/decoder-threads/probe/DecoderProbe.dll E:/ProjectSekai/test .codex-build/decoder-threads/full-4.json FfmpegThreads4 0
```

全视频逐帧一致仍需补充定位、检查点恢复及完整状态和 ASS 验证，随后再做独立处理链重复测量。FFmpeg 线程控制不作为 OpenCV 5 独有新特性；它用于探索解码与新版 IPP 后端的资源配合。

线程候选还会在顺序读取后定位到视频约 0%、25%、50%、75%、95% 处，各比较三帧。每次定位必须成功，读出的实际帧索引必须等于目标索引加读取偏移；像素和时间码差值计入报告中的最大差值。`SeekChecks` 保存定位目标及检查帧数。该检查只覆盖选定的定位点，不能替代识别恢复资格。

```powershell
dotnet .codex-build/decoder-threads/probe-seek/DecoderProbe.dll E:/ProjectSekai/test .codex-build/decoder-threads/seek-prefix-4.json FfmpegThreads4
```

输入也可为单份视频，无需复制到临时目录。顺序读取诊断记录 `SequentialPixelDifferenceFrames`、`FirstSequentialDifferenceFrame` 和最多 16 个 `SequentialDifferenceSamples`。这些索引是从 1 开始的读取序号，仅统计顺序读取，定位读取仍计入总体最大差值。报告另外记录进程可见的 `LogicalProcessors`，不将其当作 OpenCV 实际线程数。

```powershell
dotnet .codex-build/decoder-threads/probe-differences-v3/DecoderProbe.dll E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/decoder-threads/short-full-4.json FfmpegThreads4 0
```

探针同时用独立逐字节比较复核顺序读取的首帧、每 1024 帧及前 16 个 OpenCV 正差异帧，并复核所有定位读取帧。`ByteAudits` 记录样本索引、是否定位、OpenCV 最大差值、字节最大差值及不等字节数；`NormByteDisagreements` 非零时结果标记为 Inconclusive，不能用于通过或归因。该抽样不证明未复核帧的字节一致性。

字节复核器仅接受同形状 8 位图像，用 Mat.GetData(false) 获得有效像素数据后在托管代码逐字节计算，不将行填充计入比较。测试覆盖非连续 ROI、有效像素之外的不同存储、已知最大差值 255 与不等字节数，以及形状不匹配。此复核仅增强研究工具证据，不进入产品识别算法。

第五个参数可传入从 1 开始的读取序号，以逗号分隔，将其加入字节复核采样。顺序读取仍从视频开头开始，不以 seek 替代前置解码；报告 `RequestedByteAuditFrames` 与实际 `ByteAudits` 用于核对请求帧是否读到。超出读取上限的请求不会产生审计，不能宣称已验证。

```powershell
dotnet .codex-build/decoder-threads/probe-targeted/DecoderProbe.dll E:/ProjectSekai/test/daiyuping/wl_wonder_01_07.mp4 .codex-build/decoder-threads/wl-8671-round-1.json FfmpegThreads16 8700 8670,8671,8672
```
