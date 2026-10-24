# SekaiTools macOS 开发客户端

仅 Apple Silicon，macOS 15+。这是首个移植切片，不是已验收的正式 macOS 版本。范围与缺口见 [移植说明](../docs/macos-port.md)。

在 Apple Silicon Mac 安装 Xcode Command Line Tools（Swift 6+）与 .NET 10 SDK，然后在仓库根目录运行：

```bash
bash SekaiToolsMac/build-dev.sh
open artifacts/macos/SekaiTools.app
```

开发构建不签名、不公证，不用于正式分发。不要将 Windows 发布目录的 DLL 整理逻辑套用到 Host。
打包目录必须同时包含 `Contents/Helpers` 下完整的 .NET 发布产物和 `network-endpoints.json`。
脚本只在本机 Apple Silicon 构建，避免宿主系统条件错误地引入 Windows runtime。

也可分别启动：

```bash
dotnet publish SekaiToolsHost/SekaiToolsHost.csproj -c Release -r osx-arm64 --self-contained true -o artifacts/macos/host
export SEKAITOOLS_HOST_PATH="$PWD/artifacts/macos/host/SekaiToolsHost"
swift run --package-path SekaiToolsMac
```

下载页选择数据源与分类后刷新目录，搜索并保存 JSON；翻译页载入 JSON/asset，可导入旧格式或 v2 翻译，编辑并保存 v2 文本。页面切换保留编辑，替换内容及退出检查未保存修改。
轴机页选择视频、剧本和可选翻译，开始前选择 ASS 输出路径；可停止保存进度，并显式选择恢复。普通视频播放使用内嵌 AVPlayer，当前尚未与识别帧同步。

macOS 15/26 上，官方 Emgu mini 5.0.0.6584 库因最低版本为 27.0 而禁用轴机。获得以 macOS 15 为部署目标编译的 ARM64 cvextern 后，可在开发时指定：

```bash
export SEKAITOOLS_CVEXTERN_PATH="/absolute/path/to/libcvextern.dylib"
```

设置变量后需要重启客户端。此覆盖入口不验证 ABI、依赖或签名，也不表示通过实机验收；应先在目标机器完成独立探针验证。
不要求用户安装 FFmpeg/VapourSynth 来使用下载与翻译；首版不提供压制。

Windows 上可以验证 Host 的真实协议往返：

```powershell
dotnet build SekaiToolsHost/SekaiToolsHost.csproj
./Tools/VerifyMacHostProtocol.ps1
```
