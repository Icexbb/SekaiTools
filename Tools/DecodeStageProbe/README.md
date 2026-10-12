# 解码阶段诊断

只读研究工具，拆分现有软件 FFmpeg `Read` 的 `Grab`／`Retrieve` 调用。这些 API 并非 OpenCV 5 新增；用途是定位新版运行库处理链的瓶颈，不改变产品捕获器、算法或用户配置。

```powershell
dotnet publish Tools/DecodeStageProbe -c Release -o .codex-build/decode-stages/tool
dotnet .codex-build/decode-stages/tool/DecodeStageProbe.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/decode-stages/april-prefix.json verify 64 16
dotnet .codex-build/decode-stages/tool/DecodeStageProbe.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/decode-stages/april-split.json split 0 16
```

参数为视频、新输出 JSON、模式、帧数上限（0 读至 EOF）、解码线程数。拒绝覆盖证据。模式 `verify` 使用两个捕获器，逐帧对比 Read 与 Grab＋Retrieve 的独立有效像素字节、索引及时间码；差异写入报告并返回非零。它没有性能结论。`split` 和 `read` 每次只使用一个捕获器，分别报告拆分阶段或原 Read 调用累计耗时及循环总耗时，不包含识别、灰度转换、预读或预览。

第六个参数可选 `Bgr`（默认）或 `Bgra`。BGRA 使用当前 5.0 后端的 CAP_PROP_FORMAT=CV_8UC4，通过 CvInvoke.MakeType 生成类型，不硬编码旧版类型值；核对实际格式和四通道输出。验证模式去掉 Alpha 后独立比较全部 BGR 字节，分别执行 Bgr2Gray／Bgra2Gray 并比较灰度字节，同时要求这些无透明素材的 Alpha 全为 255。此验证不能替代完整识别或预览资格。

```powershell
dotnet .codex-build/decode-stages/tool-bgra/DecodeStageProbe.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/decode-stages/april-bgra-prefix.json verify 64 16 Bgra
```

原包的八视频共 512 帧 BGRA 前缀检查全部通过，实际格式为 96、四通道，加载运行库指纹与 BGR 参考相同。三组完整 BGR／BGRA 单次阶段计时已结束，BGRA 三组均较慢；证据及独立审计在 `.codex-build/decode-stages/bgra-timing`。这仍是单轮筛选，不宣称稳定退化，产品格式未改变。

报告记录实际后端、线程、帧数、是否 EOF、图像形状、输入 SHA256，以及实际加载 cvextern／FFmpeg 插件的路径和 SHA256。EOF 时最后一次 Read 可能清空 Mat，报告保留最后成功帧通道数并从捕获器读取视频尺寸。标准视频 EOF 必须达到声明帧数；前缀必须达到预期帧数。

八份标准视频各前 64 帧，合计 512 帧的字节、时间码、索引全部一致，实际原运行库指纹已核对；不覆盖未读取帧或任意运行。短视频全 Read／split 均读出 3637 帧并到 EOF，形状为 1920×1440、3 通道。三份代表视频的全阶段单次诊断已完成，输入和二进制指纹前后相同，证据位于 `.codex-build/decode-stages/{prefix,timing}`。

拆分调用存在额外托管调用及计时开销；阶段包含后端内部工作，不能仅由 Retrieve 耗时断言某个转换函数占比。单次纯解码时间不能用来宣称识别加速、替代整体识别资格或稳定性能复测。现有偶发全视频像素差异证据仍保留。
