# OpenCV 运行时能力检查

记录当前 Emgu CV 实际加载版本的 CMake 构建信息、CPU 优化开关、默认线程数、OpenCL 可用性和默认设备，以及工具目录中的 DLL 哈希。只查询能力，不切换优化开关或设备，不读取视频或写入用户设置。

```powershell
dotnet publish Tools/OpenCvCapabilities -c Release -o .codex-build/capabilities/tool
dotnet .codex-build/capabilities/tool/OpenCvCapabilities.dll .codex-build/capabilities/result
```

`build.txt` 是原始 OpenCV 构建信息；`capabilities.json` 记录运行时查询。OpenCL 设备查询失败会单独报告异常，不会伪装成支持。可用性和启用状态不能证明某个算子或真实任务获得加速，必须继续验证实际执行路径、字幕一致性及耗时。此检查会初始化 OpenCV/OpenCL 上下文，应在正式性能测试之外运行。

`ManagedCpuFeatures` 查询 .NET 进程能执行的 SSE／AVX／FMA 指令，不能证明原生 OpenCV 编入或选用了这些路径。`LoadedNativeLibraries` 记录进程实际加载的 cvextern 路径与哈希，读取失败会报告 `LoadedNativeError`。研究自编译运行库时，必须核对加载路径、哈希和原始构建信息，避免把硬件支持误认成编译配置，或仍在测量原 NuGet DLL。

自编译研究库可提供只读 sekaiExperimentInfo 接口，报告 NativeExperimentInfo（原生 CPU 特征及 IPP 实际状态、版本、特征掩码）。接口不存在时为 null，读取异常由 NativeExperimentError 记录；同时保留 IppEnvironment，用于对照请求模式与原生响应。诊断会初始化原生 IPP 状态，必须在性能作业之外用独立进程执行。
