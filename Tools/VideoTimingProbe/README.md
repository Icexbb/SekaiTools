# C# 纯视频时间轴及 WPF 验收

输入视频、已有的 `menu-107px.png` 和新输出目录，不读取同名剧本/翻译，不保存应用历史。CLI 直接调用应用同一 `VideoTimingRecognizer`。`--ui` 离屏调用真实字幕任务，并在现有对白行填译文、调整时间、导出 ASS，验证空原文不被伪造、空译文不被导出、实验图标可加载和取消可结束。UI 模式目前以 10 句的短视频为固定验收素材。

```powershell
dotnet build Tools/VideoTimingProbe -c Release -o .codex-build/single-video/native-tool
dotnet .codex-build/single-video/native-tool/VideoTimingProbe.dll E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 C:/Users/icexb/SekaiTools/Resource/videoProcess/menu-107px.png .codex-build/single-video/new-native
dotnet .codex-build/single-video/native-tool/VideoTimingProbe.dll E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 C:/Users/icexb/SekaiTools/Resource/videoProcess/menu-107px.png .codex-build/single-video/new-ui --ui
python Tools/SingleVideoProbe/compare_timing.py .codex-build/single-video/new-native/recognition.json .codex-build/single-video/reference-short/state-1.json .codex-build/single-video/new-comparison.json
```

CLI 结果保存至 `recognition.json`，用于独立时间重叠对比；WPF 模式保存 `editor.png` 和 `edited.ass`。离屏验证不等价于真实桌面点击、不同 DPI 和主题下的完整人工验收。工具读取当前应用设置但不修改设置。

WPF 回归同时检查：仅视频模式隐藏剧本、翻译及同名文件选择组件，切回普通模式后恢复；识别过程中持续产生画面预览，关闭预览后停止更新，再开启后恢复。

CLI 增加 `--diagnostics` 可保存 `observations.jsonl`，逐采样记录帧号、名字/正文像素数、菜单、横线、最终可见性和正文保留比例。它只输出诊断，不改变阈值或分段策略。可用 `Tools/SingleVideoProbe/detailed_timing_comparison.py` 在识别完成后读取剧本、参考状态和这些记录，生成逐条差异及覆盖率 CSV；这些参考信息不会反馈给识别器。

精度修复后的 `RetainedFraction` 是小范围位移补偿后的保留比例；新增 `EnoughPixels` 明确记录当前过滤规则的结果，避免分析工具沿用旧的像素阈值。横线失配但已建立跟踪时，`NameLinePresent=false` 与 `Visible=true` 可以同时成立。
