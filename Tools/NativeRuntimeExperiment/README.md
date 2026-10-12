# OpenCV 5 原生 CPU 分派研究

基于现有 Emgu 5.0.0.6584 的精确源码，构建 SSE3 控制组及 SSE4／AVX／AVX2 分派候选。保持 SSE3 基线，额外路径在支持的 CPU 上由 OpenCV 分派；不将 AVX2 提升成最低硬件要求。此能力并非 OpenCV 5 独有 API，本实验检验当前 5.0 运行包的编译配置是否限制已有流程。

固定源码：

- Emgu：`4f8dc1bf4bb562e396dedf9badaa1856deb68c29`
- OpenCV：`2b862f17fc625948d75840300a3837c4d0defa7a`
- contrib：`7bd9df363342be59052b29e8e33e42440b7ddc2e`

获取官方源码并检出锁定版本后，仅初始化 opencv 与 opencv_contrib 子模块。Windows 环境需 CMake、Visual Studio 2026 C++ 工具链和 .NET；配置会获取 OpenCV 固定的第三方构建依赖。

```powershell
git clone --depth 1 --filter=blob:none --no-checkout --branch EMGUCV_5_0 https://github.com/emgucv/emgucv.git .codex-build/native/emgu
git -C .codex-build/native/emgu fetch --depth 1 origin 4f8dc1bf4bb562e396dedf9badaa1856deb68c29
git -C .codex-build/native/emgu checkout --detach 4f8dc1bf4bb562e396dedf9badaa1856deb68c29
git -C .codex-build/native/emgu submodule update --init --depth 1 opencv opencv_contrib
./Tools/NativeRuntimeExperiment/Configure.ps1 -SourceDirectory .codex-build/native/emgu -BuildDirectory .codex-build/native/control -Variant Sse3
./Tools/NativeRuntimeExperiment/Build.ps1 -BuildDirectory .codex-build/native/control
./Tools/NativeRuntimeExperiment/Configure.ps1 -SourceDirectory .codex-build/native/emgu -BuildDirectory .codex-build/native/dispatch -Variant Avx2
./Tools/NativeRuntimeExperiment/Build.ps1 -BuildDirectory .codex-build/native/dispatch
```

两个版本串行配置、编译，避免上游在源码目录生成的文件互相干扰。模块固定为 core、imgproc、imgcodecs、videoio、geometry、highgui（geometry 会引入 flann 依赖）。highgui 同时满足现有包装层的条件编译依赖。关闭 IPP、CUDA、Eigen、GeoTIFF、OCR 及不相关图像格式依赖，只比较 CPU 分派变化。托管目标需在 CMake 生成时存在，以满足上游 MAUI 依赖，但构建命令只执行 cvextern，不执行托管／MAUI 打包。

`IsolateOutputs.cmake` 将静态库与 DLL 保存在各自构建目录；`Build.ps1` 限制一个 MSBuild 项目、四名编译器工作线程。OpenCV 静态库与 cvextern 使用动态 CRT，避免混用 MT／MD。`experiment.json` 保存配置与输出隔离脚本指纹，`build-result.json` 保存完成后的 DLL 哈希。完成构建后不允许用改变的配置复用目录；`-RefreshFailedConfiguration` 只用于尚未产生完成记录的配置修复，会保留前次配置指纹。构建结果可能不具备原运行包的全部模块，不能直接当作产品替代包。

发布诊断／基准工具，然后复制到新目录，只在该副本替换 Windows x64 cvextern：

```powershell
./Tools/NativeRuntimeExperiment/Stage.ps1 -PublishedToolDirectory .codex-build/capabilities/tool -NativeBuildDirectory .codex-build/native/control -OutputDirectory .codex-build/native/capabilities-control
dotnet .codex-build/native/capabilities-control/OpenCvCapabilities.dll .codex-build/native/capabilities-control-result
```

核对 `LoadedNativeLibraries` 的实际路径与 `build-result.json` 哈希一致，再核对 `build.txt` 的基线和分派列表。视频实验保留原发布工具中的 FFmpeg 插件和其余依赖，以免混入不同解码库；新的 staged 目录拒绝覆盖，且不得放入原发布工具目录。这里不替换 NuGet 缓存、产品 DLL、用户设置或进度。

资格验证仍需实际识别完整状态与 ASS、回归测试、取消恢复及多轮交替耗时；构建配置或硬件检查只能证明可试验，不能证明提速、等价或可发布。

真实素材筛选的标准识别命令（复制已发布工具后，只替换 cvextern）：

```powershell
./Tools/NativeRuntimeExperiment/Stage.ps1 -PublishedToolDirectory .codex-build/masked/reference -NativeBuildDirectory .codex-build/native/dispatch -OutputDirectory .codex-build/native/recognition-dispatch
dotnet .codex-build/native/recognition-dispatch/RecognitionBenchmark.dll E:\ProjectSekai\test\aprilfool_2024_01.mp4 .codex-build/native/april-dispatch 1 false Automatic false 4 false false 8 false false false
```

控制组使用相同参数，输出到另一新目录。递归查找 mp4，只选择同目录同名 json／txt 都存在的组；每换素材交换控制／候选顺序。比较 `StateHash`（包含尺度校准），同时核对实际 `state-1.json`、`subtitle-1.ass` 文件哈希及 StopReason；以 `Performance.ElapsedTime` 为识别耗时。单轮只用于筛选，稳定收益必须按独立进程多轮交替测量。正式测量期间不运行其他构建或性能作业。

本次五组筛选全部结果一致，但耗时变化混合，尚无稳定收益结论，详见 `docs/opencv5-processing-performance.md`。实验没有修改产品配置。

三轮交替测量可使用专用 runner；每次独立进程，固定自动模式、关闭预览／详细计时／额外预热，默认 4 线程／8 帧缓冲：

```powershell
./Tools/NativeRuntimeExperiment/RunComparison.ps1 -ReferenceDll .codex-build/native/recognition-sse3/RecognitionBenchmark.dll -CandidateDll .codex-build/native/recognition-avx2/RecognitionBenchmark.dll -OutputDirectory .codex-build/native/interleaved -ResourceDirectory "$([Environment]::GetFolderPath('UserProfile'))/SekaiTools/Resource/videoProcess" -Videos E:\ProjectSekai\test\1963_areatalk_ev_street_16_003.mp4,E:\ProjectSekai\test\aprilfool_2024_01.mp4,E:\ProjectSekai\test\event200_tsukasa_01.mp4
```

`ResourceDirectory` 必须指向实际 ResourceManager 使用的用户资源目录，不能借此切换资源。manifest 保存输入三件套、全部 DLL／JSON 依赖、模板资源、参数与 OPENCV_CPU_DISABLE 指纹。复用已完成运行前再次检查身份与输出文件哈希；未完成的目录拒绝自动重启，应先检查原进程与日志。轮次之间交换先后顺序，报告完整状态、ASS、结束原因必须一致。配对降幅中位数与两组耗时中位数之比是不同指标，summary 分别记录原始配对值及中位数。

保持其他 OpenCV 环境变量与机器负载不变；本 runner 尚不支持两种模式使用不同环境变量或执行系统负载审计。外部修改源文件或实验目录须重新复核来源，manifest 不是完整构建供应链证明。它适用于识别链资格测量，不测量完整 GUI 交互。

`IppAvx2` 变体使用同一源码、模块与 CPU 分派，启用 WITH_IPP；OpenCV 5 会同时接入 IPP HAL 和既有 IPP 路径，不能把整体差异全部归因于 HAL。仅该变体链接 `Diagnostics.cpp` 的只读研究接口，记录源文件哈希。接口查询这份 cvextern 内同一 core 的 CPU 特征、IPP 启用／非精确状态、版本和特征掩码，不设置优化开关。通过 OpenCvCapabilities 动态检测接口，原运行库没有该接口时返回 null。

```powershell
./Tools/NativeRuntimeExperiment/Configure.ps1 -SourceDirectory .codex-build/native/emgu -BuildDirectory .codex-build/native/ipp-avx2-info -Variant IppAvx2
./Tools/NativeRuntimeExperiment/Build.ps1 -BuildDirectory .codex-build/native/ipp-avx2-info
```

默认、OPENCV_IPP=avx2、OPENCV_IPP=disabled 均需新进程；运行前令 OPENCV_IPP_NE=0，并将环境变量的修改限于实验进程，退出时恢复。先用诊断接口确认实际模式和 DLL 哈希，再识别视频；诊断本身不在正式性能测量内。IPP 为可选后端，不能仅根据构建或启用状态宣布某个热点已走 IPP。

`GrayCapture.cpp` 是通过 Configure.ps1 的 -GrayCapture 开关接入隔离构建的原生研究原型，尚未接入产品，已用固定源码头文件在 MSVC x64 下独立编译。工厂接收调用方已核验的插件 API 指针；调用方必须保持插件库加载直至捕获对象释放。它通过 IVideoCapture 同步回调将借用 BGR 转为拥有存储的灰度输出，转发定位、抓帧及元数据，拒绝格式切换。工厂捕获异常并返回错误文本；回调捕获异常，返回插件后再抛出。后续必须在同一 cvextern 内链接，验证实际读帧、定位、释放、完整识别状态与字幕，再进行多轮计时；当前编译成功不能证明这些运行时行为。灰度预览及 PrepareGrayFrames 路径尚未适配，不得直接作为产品捕获器使用。

`PeriodicColorCapture.cpp` 是已编译并通过八个视频取帧资格验证的彩色预览研究原型，完整识别与 GUI 资格仍待验证。使用独立 `-PeriodicColorCapture` 配置开关，不能与 `-GrayCapture` 同时使用；其源码哈希独立记录并由 Build.ps1 复核，必须选新构建目录。它复用隔离目标的源文件入口，不改动已验证的 GrayCapture.cpp。

调用方需传入与 VideoProcessor 相同的预览间隔 `Math.Max(1, (int)Math.Round(fps / 5d))`。按捕获器实际 post-grab PosFrames 的绝对索引取模，候选帧复制为拥有存储的 BGR，其他帧转换为拥有存储的灰度，定位后不重置颜色周期。Format 报告仅代表彩色能力，逐帧真实通道数可能变化，调用方必须按 Mat 的通道处理；PrepareGrayFrames 仍不可启用。现有灰度工具包装器不兼容这个工厂，必须新增对应包装和实验身份，不能将旧灰度报告用于证明该原型。

后续检查必须覆盖候选帧与原路径 BGR 像素、所有帧灰度像素、索引和时间、周期边界 seek、断点恢复、借用缓冲释放及实际彩色预览回调，再核验完整识别状态和 ASS。原型和配置脚本准备完成不等于构建、运行或 GUI 验证通过。

`CompareExports.py` 只读解析 x64 PE 导出名称及直接导入 DLL，不加载运行库。原包与周期彩色实验运行库对比证据 `.codex-build/periodic-wpf/native-export-compatibility.json`：7946／4342 个导出，缺少 3606 个 C++ 符号，新增两个研究接口；`cve*` 导出名称未缺失。名称保留不能证明调用可用，精简构建会保留未编译模块的包装入口；名称比较也不验证签名、布局、行为或完整 ABI。正式替换仍需功能能力、产品实际调用和发布布局验证，不能凭取帧／WPF 局部通过宣布完整原生包兼容。

Stage.ps1 同时替换发布根目录、libs 及 runtimes/win-x64/native 的 cvextern 实物，忽略其他架构。GUI 发布整理会将根目录原生 DLL 移到 libs，必须涵盖这个实际加载位置。PublishedLayoutProbe 的首个副本由于旧脚本遗漏 libs 而加载原包，找不到新工厂；修正后在新发布副本中实际加载 libs 的候选哈希，托管解析及 WPF 显示检查通过。该资格仍不证明产品捕获器已接入或完整原生包功能兼容。

扩展模块配置：Configure.ps1 可用 `-ModuleComparisonPath <CompareBuildCapabilities.py 结果>` 请求原包构建模块，核对原包指纹并拒绝非法／重复模块名，将清单哈希和 RequestedModules 写入隔离 experiment.json，包含固定 contrib 模块路径并尝试启用 TIFF／WEBP、MSMF／DirectShow、FreeType。默认不传该参数仍保留既有有限模块实验配置。请求清单不证明模块构建成功。首次扩展目录 `.codex-build/native/ipp-periodic-expanded` 配置因 PROJ 缺少 SQLite 二进制、头文件、库失败；FreeType／HarfBuzz 未找到，首次尚未添加 contrib 路径。日志 `.codex-build/expanded-configure.log` 保留；已补充 contrib 路径，下一次需使用新目录配置并补齐第三方依赖。不得将此次失败目录用于 Stage 或更新产品资格指纹。非法模块清单负例在创建构建目录前被拒绝。

固定依赖构建：PrepareDependencies.ps1 使用基线 b1e15efef6758eaa0beb0a8732cfa66f6a68a81d，在独立目录构建 x64-windows-static-md 的 SQLite（含工具）、FreeType、HarfBuzz、HDF5、Eigen；下载及二进制缓存均单独配置。失败后 `-ResumeFailed` 只接受同一清单且没有成功清单的目录，保留已完成构建。成功记录安装前缀与 vcpkg/status 的文件哈希，避免将中间源码和构建日志当作稳定交付物。

`.codex-build/native-dependencies-pinned/dependency-manifest.json` 记录 1391 文件，续跑日志 resume-v2 以成功结束；SQLite 3.50.4 工具可执行。libaec 旧项目路径两次 HTTP 429，使用上游当前 dkrz-sw 路径归档填入下载缓存，SHA512 与固定端口完全一致，记录 libaec-source.json。版本和校验未变。Configure.ps1 的 DependencyDirectory 校验安装文件及依赖清单哈希，设置独立 CMake 前缀及 SQLite 工具／库／头文件；空清单负例在创建构建目录前拒绝。扩展配置 `.codex-build/native/ipp-periodic-expanded-deps` 已启动，是否配置成功及最终模块集须以后续终端结果核验；依赖构建成功不是扩展原生库构建成功。

扩展工程生成修复：DependencyDirectory 同时载入实际 vcpkg 工具链，限定 VCPKG_INSTALLED_DIR 为隔离目录、x64-windows-static-md、关闭 manifest 安装模式，并核验 vcpkg 可执行文件哈希。首次仅设置前缀时 FreeType 的 ZLIB::ZLIB 未加载，工程生成失败；`.codex-build/native/ipp-periodic-expanded-toolchain` 修复后配置终端 exit 0，module-audit.json 的实际 OPENCV_MODULES_BUILD 与原包 56 项完全一致。原生编译已启动，日志 expanded-toolchain-build.log，不能据此宣布二进制兼容或更新产品资格指纹。

字体包含路径适配：ExpandedDependencies.cmake 在 OpenCV 项目初始化时指定隔离依赖的实际 include 和 include/harfbuzz，并检查 ft2build.h／hb.h。配置记录该文件哈希，Build.ps1 在编译前核对。原 Emgu 假设 config 在 lib/cmake 层级，与 vcpkg 的 share 布局不同，首次生成工程的字体头文件不可达；发现后停止已确认的隔离构建树，保留此前编译，重新配置 exit 0。font-include-audit.json 证明两个头文件均在生成项目搜索路径上；仍有父目录继承的无效旧 HarfBuzz 路径，但正确路径已加入。缺失头文件的 bootstrap 负例失败。修正后的编译日志 expanded-font-path-build.log，尚需等待最终二进制。

扩展产物初检：ipp-periodic-expanded-toolchain 构建 exit 0，SHA256 EC795EC4496E2DAB48A593A3E3FC94DEDC35FFB05404BF6A3D9DA7CCBFDB35D1；export-comparison.json 保留原包 7946 导出，新增 3，缺少 0。expanded-runtime-capabilities 实际加载后列出与原包相同的 56 模块，expanded-runtime-tests 下全部 266 测试通过。尚未加入产品资格指纹，完整识别／恢复／GUI、性能及更多 API 功能等价检查未完成，不能自动替换既有候选或正式发布。
