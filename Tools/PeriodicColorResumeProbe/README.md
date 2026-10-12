# 周期彩色捕获的断点恢复资格探针

研究工具，已编译并通过三个实际回查边界的取消／恢复资格验证。使用独立周期彩色工厂和实验身份，不接受旧灰度实验检查点。插件及原生库的实际路径和 SHA256、预览周期、原始配置身份全部纳入审计。

通过真实 ProgressStore 原子保存检查点，恢复前验证输入、元数据及实验身份，只在内存还原原配置身份供处理器恢复。恢复后按原周期继续取帧、生成彩色预览，核对完整最终状态、ASS 和实际历史文件与同配置连续处理一致。每次取消、恢复都必须实际收到三通道预览回调，释放捕获器指针为零；产品原配置拒绝实验检查点。

```powershell
dotnet PeriodicColorResumeProbe.dll <视频> <插件> <新的输出目录> cancel 1029 default default
dotnet PeriodicColorResumeProbe.dll <视频> <插件> <另一个新的输出目录> resume <检查点路径> default default
```

`default` 不修改匹配线程数，并保留自动缓冲。RunBoundarySeries.ps1 对 1028、1029、1030 执行独立取消／恢复进程，参考必须是同原生库、默认线程和自动缓冲、开启预览的完整周期彩色识别结果。VerifyPeriodicColorResume.py 检查所有完整状态字段和字幕字节、真实检查点及历史文件；脚本运行前后核对输入、模板、发布工具、参考结果及审计源码未变。

本工具不衡量性能，不证明 WPF 界面实际显示，也不覆盖任意历史版本检查点。PreviewQualified=false 指实际 GUI 未验证；预览回调计数和通道检查另行记录。

可选第八参数 `true` 使用实际基础设施 `PeriodicColorCaptureFactory`，监控包装器仅记录会话释放，不创建或替换捕获器。非工厂模式仍保留原研究实现。该模式直接保存并恢复 CaptureIdentity，不修改 ConfigHash 或在内存归一化身份；普通捕获身份拒绝该检查点。`RunBoundarySeries.ps1 -Factory` 要求同模式连续参考，使用 VerifyFactoryResume.py 逐字段比较完整状态，并核验真实原子检查点、历史、彩色回调和处理器释放会话。

工厂证据 `.codex-build/factory-resume/april-boundaries/summary.json`：1028、1029、1030 的六个独立进程全部通过，输入、同名剧本／翻译、模板、工具、参考与审核脚本清单未变。1029 实际覆盖 Dialog 和 Banner 的待回查帧。实际原生库为 `5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984`。CheckFactoryIdentity.ps1 的缺失身份、错误运行库、错误周期三个负例均在实际恢复兼容性校验处拒绝，未产生 state.json，源检查点未变；证据 `.codex-build/factory-resume/identity-negatives/summary.json`。这不证明任意断点、旧版进度或完整 GUI 行为。

证据 `.codex-build/periodic-resume/april-boundaries/summary.json`：1028、1029、1030 三组、六个独立进程均通过完整状态／ASS、真实原子检查点、历史文件、彩色回调、捕获释放及产品拒绝实验状态检查，输入／模板／工具／参考和审计脚本指纹未变。1029 取消状态中 Dialog 与 Banner 的 PreviousSkippedFrameIndex 都为 1029，实际覆盖待回查帧重建。默认匹配线程在本机实际为 16、缓冲为自动选择。CheckVerifier.py 的七种负例全部被拒绝，包括同步改动状态及历史并重算哈希、修改字幕并重算哈希、错误源检查点哈希、缺失预览、非彩色预览、错误周期及释放标记。仍未证明任意旧检查点或实际 WPF 界面行为。

实际产品工厂恢复复测使用 `.codex-build/product-factory-resume/` 隔离目录。报告新增 `ProductFactoryImplementation`；工厂审核器要求该标志，避免将旧研究工厂结果当作产品工厂证据。本工具仍有用于确定预览周期的元数据捕获，不用于性能测量。

实际产品工厂证据 `.codex-build/product-factory-resume/april-boundaries/summary.json`：三个边界的六个进程均通过完整状态、ASS、原子检查点／历史和会话释放审核；运行前后素材、模板、工具、参考及审核脚本指纹未变。`.codex-build/product-factory-resume/identity-negatives/summary.json` 的缺失身份、错误运行库、错误周期均在兼容性校验处拒绝，源检查点未变。旧 `.codex-build/factory-resume/` 证据使用研究工厂，不能替代本次实际产品实现验证。
