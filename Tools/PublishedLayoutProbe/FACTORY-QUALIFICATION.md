# 发布布局中的产品捕获工厂

第三参数 `factory` 将第一参数解释为已有工厂状态 JSON。调用实际 GUI SubtitleTask.GetCaptureFactory 保存身份选择路径，再调用基础设施工厂 Open，读取一个完整彩色周期及下一帧，核验通道、身份、实际 libs 加载指纹与释放指针。`factory-reject` 要求同一身份在原包运行库中于工厂 Open 资格检查处被拒绝。

本地证据 `.codex-build/periodic-gui-final/factory.json` 和 `factory-reject.json` 均通过。候选实际加载 `candidate/libs/cvextern.dll`，SHA256 为 `5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984`。原包拒绝相同身份；两包产品 GUI 程序集指纹相同。这里只读取冻结检查点和输入视频，不修改用户设置或启动主窗口。

候选是工作区内用于核验的隔离包，原生库仍为有限模块构建。此探针不证明完整主窗口、队列交互、整个视频像素或完整 SDK 兼容；LiveGuiQualified=false。正式发布必须由用户手动确认。
