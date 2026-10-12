# 彩色预览捕获取帧资格探针

研究工具，已完成编译及八个视频的取帧资格验证。依赖隔离运行库的 `sekaiCreatePeriodicColorCapture`，不适用于旧灰度工厂。输入、模板及用户进度均不修改，报告路径必须不存在。

预览间隔与 VideoProcessor 一致，取 `Math.Max(1, (int)Math.Round(fps / 5d))`。验证前 96 帧及八个定位点附近帧：周期候选输出三通道、其他输出单通道；分别与参考 BGR 或灰度逐字节比较，并经实际 FrameMatchContext 验证所有帧的识别灰度相同。核对帧索引、时间、帧率、总帧数及 EOF。

另保留真实返回的彩色和灰度 Mat，在两轮颜色周期的后续读帧过程中检查其内容不变，核验输出拥有独立存储；间隔为 1 时没有灰度帧，因此报告不会声称双通道存储检查通过。捕获器释放后验证指针为零；插件在捕获器释放后卸载。记录视频及实际运行库、插件的路径和 SHA256，前后核对视频未变。

```powershell
dotnet PeriodicColorCaptureProbe.dll <视频> <已核验插件路径> <新的报告路径>
```

此探针不衡量性能，也不能证明完整识别、断点恢复或 WPF 实际彩色预览行为；这些需要后续独立验证。

可选第四参数 `public-bind` 使用公开的 `VideoCapture(string)` 构造和 `Ptr`，再调用隔离原生库的 `sekaiBindPeriodicColorCapture`，不调用 Emgu 内部指针构造函数。原生入口先创建新后端，再赋值给原捕获器；插件模块仍由外层所有者保留到捕获器释放。此路径会先打开一次普通捕获器，再打开周期捕获器，启动开销尚未计入既有性能结论。

公开绑定模式还尝试非法间隔和不存在的视频路径，要求绑定返回失败及错误消息，保留原指针、打开状态、帧位置和总帧数；随后执行同一套像素和定位验证，检查失败后仍可正常读取。批量脚本加 `-PublicBinding` 会要求报告确认该路径及失败保护检查，沿用完整输入、工具和原生身份清单。此模式使用新的运行库，不能沿用旧运行库的完整识别或恢复资格。

公开绑定八组证据为 `.codex-build/periodic-binding/qualification/summary.json`：960 帧、64 个定位点、失败绑定后的连续读取、持有帧存储及释放检查全部通过，运行前后清单未变。实际原生库 SHA256 为 `5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984`，构建身份 SHA256 为 `B81988633DD8757941FC4B34FE8F013927BA3A01B61891A66BF42E53C000F192`。产品主处理器尚未接入此绑定路径。

随后主处理器增加 `IVideoCaptureFactory`／`IVideoCaptureSession` 注入入口，捕获身份直接进入 ProcessingStateMetadata，捕获器及会话由处理器释放；GUI 仍使用普通构造，周期彩色未默认启用。`RecognitionBenchmark` 第十七参数 `true` 通过工厂入口使用公开绑定，覆盖完整识别、彩色回调、同身份检查点兼容、普通身份拒绝和处理器释放会话。此路径不修改 ConfigHash 来恢复检查点。

五组完整工厂识别证据为 `.codex-build/factory-recognition/five-qualified/summary.json`。short、april、event152、event200、wl 全部与冻结原包参考的完整状态及 ASS 相同，仅移除已独立验证的 CaptureIdentity 后比较状态；捕获原生指针及会话均已释放。输入、同名剧本／翻译、模板、冻结工具、参考结果及审核脚本清单前后未变。该轮不作为性能结论，也未证明工厂模式断点实际恢复、完整 GUI 交互或完整原生 SDK 兼容。

基础设施层 `PeriodicColorCaptureFactory` 随后实现实际产品工厂，在调用原生绑定前核验实际 cvextern 与插件的固定指纹，不替换运行库、不自动启用。RecognitionBenchmark 工厂模式改为调用该实现，外层仅监测会话释放。短视频证据 `.codex-build/product-factory/short` 已通过完整状态／字幕与释放核验。该验证工具仍额外创建一个实验捕获器以记录原生身份，因此这轮不能用于产品工厂性能或内存结论；发布集成前需移除额外诊断捕获器并重新测量。

之后移除工厂模式的额外实验捕获器及预览周期元数据捕获器，周期由主处理器传入，报告指纹直接来自实际加载模块。`.codex-build/product-factory-clean/short` 通过相同完整状态、字幕及会话释放核验。该单次资格检查仍不作为性能结论；旧非工厂研究模式保持原路径，历史证据仍对应当时冻结工具。


八组验证证据为 `.codex-build/periodic-color/qualification/summary.json`：960 帧、64 个定位点均通过，包含中文文件名、周期边界和末尾；彩色像素及实际识别灰度相同、索引时间相同、保留输出帧未变、释放指针为零。实际原生库 SHA256 为 `3EB5B552A4481508E949913F84AD85F13E4F3FB2EB33180F2260AB2CCEDF73A7`。运行结束输入、工具、原生构建身份及源码清单均未变。结果只覆盖所测前缀和定位附近帧，不证明整个视频所有像素、完整识别或实际 GUI 预览。
