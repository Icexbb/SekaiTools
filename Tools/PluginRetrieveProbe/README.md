# FFmpeg 插件同步检索回调诊断

只读隔离研究工具，不替换产品解码器、不改变匹配算法。使用现有插件的捕获 ABI v1／API v1 接口直接取帧，测量后端检索与外层回调操作的成本。这套插件接口本身不是 OpenCV 5 独有的性能特性，也不是可发布的解码器抽象。

只支持已核对的 Windows x64 布局、当前固定 FFmpeg 插件 SHA256 `7AABFF029D1FA47CF56EDE4DBBE05DC62DA3208F07CD16471D4C966A33F9AA31`；加载前拒绝其他哈希，初始化后核对结构大小、API 版本、FFmpeg 后端和必要函数指针。当前插件报告 5.0.0／API 2。像素指针只在同步回调内包装为借用 Mat，回调结束前释放包装头；仅拥有的复制颜色图或灰度图可以保留。回调异常转换为错误返回，返回托管调用后重新抛出，不跨原生边界传播。

```powershell
dotnet publish Tools/PluginRetrieveProbe -c Release -o .codex-build/plugin-retrieve/tool
dotnet .codex-build/plugin-retrieve/tool/PluginRetrieveProbe.dll E:/ProjectSekai/test/aprilfool_2024_01.mp4 .codex-build/plugin-retrieve/tool/runtimes/win-x64/native/opencv_videoio_ffmpeg500_64.dll .codex-build/plugin-retrieve/april-prefix.json verify 64 16
```

参数为视频、已限定插件 DLL、新输出 JSON、模式、帧数上限（0 为 EOF）及解码线程数。拒绝覆盖输出，核对实际线程、软件模式、索引和 EOF 声明帧数。

- `verify`：与正常 Read 比较颜色／灰度有效字节、索引、时间码；两个捕获器及字节比较使此模式没有性能结论。
- `copy`：同步回调只复制完整 BGR 输出，近似观察正常外层输出拷贝成本。
- `gray`：在同步回调将借用 BGR 图转为拥有的灰度图，避免外层 BGR 复制，测量这种数据路径的资格线索。
- `discard`：回调只验证形状，不生成外层输出；插件内部转换、分配及返回图像的工作仍然存在，不能称为无转换或零拷贝解码。

报告的 Retrieve 包含回调，CallbackSeconds 是其中的子区间，不能将两者相加；差值仍包含插件内部工作和调用开销，不直接等价于 swscale 时间。灰度模式把处理移到同步检索线程，不是完整识别流水线提速证明。报告包括输入和实际加载原生库指纹。

八份素材各前 64 帧，共 512 帧颜色、灰度、时间码和索引一致，运行库指纹已审核；不覆盖所有未比较帧或恢复边界。修改隔离插件副本一个字节被拒绝，未调用其 ABI。三份代表视频各三种完整诊断模式已完成，输入和运行库指纹不变，结果位于 `.codex-build/plugin-retrieve/{prefix,timing}`；单次计时只用于定位和筛选，候选仍需完整状态、字幕、恢复及重复性能验证。

接口布局依据锁定源码的 `plugin_capture_api.hpp`、`llapi.h` 和 `backend_plugin.cpp`；OpenCV 将低层接口标为预览、没有向后兼容承诺。不要在未知插件或产品依赖中复用该绑定。

验证模式另记录按帧串接的 ColorFingerprint／GrayFingerprint。八份素材各 64 帧在原包和隔离 IPP 库各执行一次，共 16 次前缀诊断，颜色及灰度指纹跨运行库全部相同，各自实际库哈希已复核；证据为 `.codex-build/plugin-retrieve/fingerprint-prefix/audit-summary.json`。这仅覆盖前缀，不能由指纹相同推出完整处理等价。

三代表视频的 IPP 同步灰度全 EOF 单次诊断已完成：short 3637 帧用时 6.21 秒、april 16389 帧用时 37.98 秒、event200 19191 帧用时 47.94 秒；累计回调分别为 1.02、4.77、7.50 秒。输入及工具清单哈希复核未变，实际加载指定 IPP 库，报告字段与 EOF 帧数核对通过，审计保存于 `.codex-build/plugin-retrieve/ipp-gray-timing/audit-summary.json`。相对先前原包灰度单次 7.11、40.16、53.02 秒，这提供原型筛选线索；跨批次单次比较不能证明稳定提速。完整识别状态、字幕、断点恢复及预览尚未验证，产品默认路径保持原状。
