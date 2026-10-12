# 带掩码匹配的浮点预处理实验

```powershell
dotnet publish Tools/MaskedMatchingProbe -c Release -o .codex-build/masked/probe
dotnet .codex-build/masked/probe/MaskedMatchingProbe.dll E:/ProjectSekai/test .codex-build/masked/operator.json
```

递归选择具有同名 JSON、TXT 的 MP4，每份取中间帧底部四分之一区域并缩小一半。使用实际模板生成器，根据首条对话的原文姓名和正文生成模板，正文最多取 16 个 UTF-16 代码单元，覆盖 1、0.96、1.04 三种尺度。ROI 与文本选择是固定的算子探测条件，并非复现某帧的生产识别搜索区域。

比较原 8 位图像／模板／掩码与预转换 32 位浮点模板／二值浮点掩码。候选每次仍转换图像，计入耗时；透明掩码严格保持 8 位非零即选中的语义，不按原 alpha 值计算浮点权重。转换在原有灰度缩放完成之后进行。

每个条件先各调用一次，再执行三轮、每轮每种路径 50 次，交替先后顺序，固定四线程。报告保留各轮单次平均耗时及中位数，单独记录图像转换耗时。首次模板预处理、解码、资源加载和结果验证不在算子计时中，不能当作真实任务提速或直接扣除转换时间来预测总耗时。

计时前后逐位检查整个原始浮点分数矩阵，包括 NaN 和无穷值的表示；任何差异都会失败。探测通过也不能证明全视频字幕一致，进入产品前仍需完整状态、字幕、取消恢复与生命周期验证。该工具不改产品匹配算法、用户进度或设置。运行时能力可用 `Tools/OpenCvCapabilities` 另行记录。

## 实际识别实验补丁

`PreparedMatching.patch` 保存基于 `b1e8c82` 的实验：按尺度缓存浮点模板和二值掩码，任务内复用图像转换缓冲，并增加五个测试条件。默认路径关闭候选，仅独立 RecognitionBenchmark 的第十四个参数开启。补丁没有进入产品，因真实素材筛选没有值得继续资格测量的提速迹象。

在独立研究检出中先发布原版本，再应用补丁发布候选：

```powershell
dotnet publish Tools/RecognitionBenchmark -c Release -o .codex-build/masked/reference
git apply --check Tools/MaskedMatchingProbe/PreparedMatching.patch
git apply Tools/MaskedMatchingProbe/PreparedMatching.patch
dotnet test SekaiTools.sln -c Release
dotnet publish Tools/RecognitionBenchmark -c Release -o .codex-build/masked/candidate
dotnet .codex-build/masked/reference/RecognitionBenchmark.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/masked/reference-run 1 false Automatic false 4 false false 8 false false false
dotnet .codex-build/masked/candidate/RecognitionBenchmark.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/masked/candidate-run 1 false Automatic false 4 false false 8 false false false true
```

核对候选报告 `PreparedTemplates: true`，再比较 `StateHash` 与 `SubtitleHash`，并核对实际状态、ASS 文件。参考版本不能在应用补丁之后重新构建覆盖。该实验输出均为隔离结果，未验证取消恢复，不用于用户进度或历史导入。
