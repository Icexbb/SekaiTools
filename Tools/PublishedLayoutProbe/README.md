# 发布布局加载检查

已编译并在隔离 GUI 发布副本验证。探针置于发布根目录，以产品 SekaiToolsGUI.deps.json 和 runtimeconfig 执行；显示探针 DLL 置于 libs。运行实际产品模块初始化器注册 AssemblyResolver，但不创建 App、不启动窗口、不访问用户任务。

调用 PeriodicColorWpfProbe 验证实际转换和离屏显示，检查 Emgu.CV、Emgu.CV.Wpf、Core 均从发布 libs 加载；实际 cvextern 路径必须在 native-experiment.json 的替换清单内，实物哈希等于候选构建哈希。记录实际路径、哈希和显示报告指纹。

初次试验发现 GUI 的 OrganizeOutput 将根目录 cvextern 移到 libs，而旧 Stage.ps1 未替换该位置，导致实际加载原包、找不到周期彩色工厂。已修正 Stage.ps1，并在新副本 candidate-v2 通过，证据 `.codex-build/periodic-gui/april-display-v2.json.layout.json`、对应显示报告和 PNG。首个失败副本保留。

这是添加研究探针后的发布副本检查。ProductPeriodicCaptureIntegrated 和 LiveGuiQualified 均为 false：产品捕获构造尚未接入该工厂，窗口交互和完整功能未验证，不能据此替换正式安装。

新增 timeline 模式调用真实产品 TimelineFramePreviewLoader，使用同一个 GUI 程序集及产品解析器。五组、十个独立进程验证七个单帧位置、连续双帧和重复双帧，图像尺寸、格式及全部像素指纹相同；图像冻结、无效帧、取消、组件释放及实际原生指针归零均检查通过。RunTimelineQualification.ps1 在运行前后核对输入、两个发布包、探针和脚本源码清单未变，证据 `.codex-build/periodic-gui/timeline-qualified/summary.json`。实际原包／候选原生哈希均严格核对。此资格证明所测产品时间轴调用可用，不证明其余未调用模块或产品主识别已改用周期彩色工厂。
