# 翻译原始语音预览

行头编号下方的按钮播放原始角色语音；同一行可暂停和继续，其他行会结束先前播放。加载时显示文字和图标，再次点击取消；失败原因显示在提示中，再次点击重试。无语音或缺少剧本标识时禁用。离开页面或重建翻译项目时停止播放。

参考 PRSK_Editor `Main.pyw::playVoice`，取 `TalkData[index].Voices` 的第一条及其音量，Sekai Best 使用 `{ScenarioId}_rip/{VoiceId}.mp3`，其他数据源使用 `{ScenarioId}/{VoiceId}.mp3`，路径位于所选下载源的 `sound/scenario/voice/` 下。WPF 使用 MediaPlayer，复用当前网络代理及 Fetcher；不会执行 Qt 的阻塞下载循环。

缓存位于本机 LocalAppData/SekaiTools/VoiceCache，按完整资源 URL 的 SHA-256 隔离数据源和剧本，下载完整后原子替换。媒体解码失败后移除缓存以允许重新下载。播放状态属于行模型；页面维护单一播放器，取消请求并检查请求身份及播放器身份，过期回调不触发播放。

验证：529 项单元测试通过，包括资源路径、二进制缓存、剧本缓存隔离及取消清理；TranslationEditingProbe 验证播放图标、控件复用后点击当前行、无语音禁用，以及既有翻译编辑回归。GUI 构建通过，保留已有 NU1701 警告。真实远程语音和设备输出尚未人工试听。
