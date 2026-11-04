# 地点动画边界复核

读取视频和已知的原识别起止帧，单独运行产品中的边界精修。帧号从零开始。
不下载资源，不写应用历史、进度或输入视频。

```powershell
dotnet build Tools/BannerTimingProbe -c Release -m:1 -o .codex-build/issue63/probe
dotnet .codex-build/issue63/probe/BannerTimingProbe.dll <video.mp4> 328 471 13488 13629
```

输出原区间、是否成功精修以及遮罩、文字分别导出的帧号和 ASS 时间。
这仅验证指定事件边界，不替代完整识别和字幕叠加验收。
