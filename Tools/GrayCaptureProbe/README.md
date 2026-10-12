# 同步灰度原生捕获资格检查

只读研究工具，使用隔离 cvextern 的 sekaiCreateGrayCapture 工厂。仅 Windows x64，加载前核对固定 FFmpeg 插件哈希；插件库保持加载直至捕获器释放。通过 Emgu 内部指针构造器包装同一运行库分配的 VideoCapture，检查正常 Dispose 后指针归零。该工具依赖内部接口，不能作为产品捕获抽象发布。

调用：`GrayCaptureProbe <video> <qualified-plugin> <new-report>`。报告拒绝覆盖。相同运行库内普通 BGR 捕获先转灰度，与原生回调灰度比较有效像素字节、帧号和时间码；比较前 64 帧，以及 0、四分之一、二分之一、四分之三、倒数第三帧五处各三帧。这里只验证局部取帧、定位与指针释放，不测性能，也不证明全视频或跨运行库像素完全一致。

本次八份测试视频全部通过，每份 79 帧，共 632 帧、40 个定位点；报告位于 `.codex-build/gray-capture/*prefix.json`。实际灰度原型 cvextern SHA256 为 `51E9715A0686CA8BF21E185AF652938A44B7C7E52571C1054AF7325F7703C1DE`，FFmpeg 插件 SHA256 为 `7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31`。尚需完整识别状态、ASS、断点恢复、预览与多轮性能检查。
