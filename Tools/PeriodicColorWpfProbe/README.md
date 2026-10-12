# 周期彩色捕获的离屏 WPF 显示检查

研究探针，已编译并通过 short、april 两组运行检查。使用与 SubtitleTask 相同的 Dispatcher.Invoke 和 Emgu ToBitmapSource 转换，并反射调用产品 FramePreview.Create 缩放规则。每个视频检查八个实际彩色候选帧：参考与候选的 BitmapSource 尺寸、格式和像素一致；放入 WPF Image，经 Measure、Arrange、UpdateLayout 后 RenderTargetBitmap 的像素一致。

转换后先释放源 Mat，检查 BitmapSource 仍可读和渲染，符合 GUI 保留显示图像的生命周期。最后保存候选渲染 PNG，记录视频、实际原生库、插件和 WPF 转换程序集的 SHA256。报告和 PNG 必须使用新路径，输入前后哈希一致；捕获器释放指针为零。

```powershell
dotnet PeriodicColorWpfProbe.dll <视频> <插件> <新的报告路径>
```

此探针运行 STA 和真实 WPF 转换／离屏 Image 渲染，但不开实际产品窗口。它不证明窗口交互、预览开关、任务队列或发布包行为，也不衡量性能。报告的 LiveGuiQualified 始终为 false；不得用离屏通过代替完整 GUI 资格。

两组共 16 个预览候选帧均通过，证据 `.codex-build/periodic-wpf/short.json`、`april.json` 及同名 PNG。输入前后哈希相同，实际原生库为 3EB5B552A4481508E949913F84AD85F13E4F3FB2EB33180F2260AB2CCEDF73A7，WPF 转换程序集哈希 D35C3490AD534FCADD5CE716272F5019B4594A45CAD97A953210B2849FFCAC27；捕获释放后仍可保存渲染图。april PNG 已目视确认显示彩色背景与文本。此局部结果不覆盖任意窗口 DPI、主题、显示尺寸或真实任务交互。
