# 下载队列 WPF 验证

先构建 GUI，再构建此轻量工具；工具运行时从指定 GUI 目录加载现有程序集，不另复制原生依赖。

```powershell
dotnet build SekaiToolsGUI/SekaiToolsGUI.csproj -c Release
dotnet build Tools/DownloadUxProbe/DownloadUxProbe.csproj -c Release -o .codex-build/download-ux/probe
dotnet .codex-build/download-ux/probe/DownloadUxProbe.dll SekaiToolsGUI/bin/Release/net10.0-windows10.0.19041.0/win-x64/SekaiToolsGUI.dll .codex-build/download-ux/new-layout
```

输出目录必须全新。工具载入真实主题及下载组件，离屏生成截图；使用离线列表占位，任务请求由可控假实现替代，不连接数据源、不写下载目录。覆盖亮／暗主题、1280×900、800×900、800×540 窗口，核对实际尺寸、底部操作可见及响应布局。主题在真实设置懒加载之后再次施加并检查，防止保存的主题干扰测试标签。

交互部分触发真实开始／停止按钮，验证三并发运行期间新增任务留到下一批、允许继续浏览、目录与来源固定、停止不丢成功项、继续不重复成功请求。`report.json` 为布局记录，`interaction.json` 为流程断言结果，截图为人工检查证据。不替代系统目录选择器、真实源站、可见窗口、DPI 或键盘操作验收。


追加 `browser` 参数可验证五类列表：

```powershell
dotnet .codex-build/download-ux/probe/DownloadUxProbe.dll SekaiToolsGUI/bin/Release/net10.0-windows10.0.19041.0/win-x64/SekaiToolsGUI.dll .codex-build/download-ux/new-browser browser
```

该模式在进程内构造五类内存夹具，原始缓存只读、不保存夹具。用真实浏览器验证文件级计数、原有筛选交集、关键词、逐项和全选加入、隐藏选择清理、队列状态、空结果和重置；每个分类每个主题快速切换搜索十轮。记录在 `browser-report.json`，图片等待动画完成再保存。主题在设置初始化之后只按主题组切换，不在每个分类窗口反复重设资源字典；源和分类导航使用固定夹具，不调用刷新或真实网络。主模式另验证运行中缩放与展开队列摘要。
