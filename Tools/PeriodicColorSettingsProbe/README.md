# 周期彩色设置隔离验证

调用实际 WPF SettingPageModel 的内部路径构造入口，设置文件只写入新的工作区输出目录。默认产品构造仍使用原用户设置路径。核验默认关闭、灰度预处理与周期彩色互斥、保存后重新加载、重置关闭、旧 JSON 缺省兼容，以及用户设置文件前后 SHA256 不变。程序创建测试 Application，不打开产品窗口，不能作为完整 GUI 交互资格。

`dotnet run --project Tools/PeriodicColorSettingsProbe -c Release -- <新的输出目录>`

证据 `.codex-build/periodic-settings/qualification/report.json` 全部通过。报告记录实际 GUI 程序集指纹；测试 Application 使用自己的资源程序集，因此不核验产品版本号或窗口主题显示。之前两次探针资源程序集初始化失败的日志保留，最终通过运行不使用这些失败结果。

随后增加实际 SettingPage、产品 WPF-UI 资源字典及同源图标资源，展开实验分组，操作真正的周期彩色 CheckBox 绑定，核验写入隔离设置文件且灰度预处理关闭。证据 `.codex-build/periodic-settings/page-qualification-v3/report.json` 和 `settings.png` 通过，截图已人工视觉核对烧杯图标、文本及勾选状态。测试 Application 的窗口资源仍独立，不打开产品主窗口；LiveGuiQualified=false。此前 v2 截图未等待展开动画，不能用于确认展开内容，保留记录但以 v3 为可视证据。

`.codex-build/periodic-settings/saved-selection-qualification/report.json` 进一步通过实际 SubtitleTask 的保存身份选择和恢复选项：无身份历史返回普通捕获，周期身份恢复禁用 BGR-only 灰度预处理，未知身份在选择处拒绝。使用合成元数据只验证 GUI 分支，不将其用于真实恢复；实际检查点恢复另有独立证据。用户设置指纹仍未变，完整窗口网络启动与队列交互仍未覆盖。

主窗口路径核验：`.codex-build/main-window-settings/qualification/report.json` 记录实际 MainWindow 创建非零原生窗口句柄、通过 NavigationView 导航到实际 SettingPage、收到 Navigated 事件，再完成异步 Closing／Closed。探针使用透明、不激活、不显示任务栏的窗口；内部构造参数禁用自动更新，清空普通初始导航项避免资源检查／进度恢复，保留设置导航。公共生产构造仍开启自动更新并保留原导航。用户设置前后哈希不变。该证据不验证正在处理的队列、预览开关或关闭时保存进度，也不证明可见窗口视觉效果。

可选第二参数为标准视频路径，启用真实 SubtitleTask 控件运行检查：收到至少两次 FramePreviewImage 更新后关闭预览，确认继续处理至少 100 帧且图像不再更新；重新开启后必须再次更新，然后请求取消，确认实际原子保存的 StopReason 为 Canceled，DisposeAsync 清空处理器。进度依赖通过内部构造参数注入隔离存储；生产公共构造仍使用原队列存储。证据 `.codex-build/active-gui-task-final/qualification/report.json`：实际加载候选 cvextern 指纹 5B13F57426105BABBFD02395D4F6AC5F9D622FAE9FD0B9C81C08CA9FD287B984，上述检查通过，用户 Data／Progress／SubtitleQueueProgress／History 文件集合及哈希未变。控件未加入实际队列或可见窗口，本次使用用户现有识别设置和普通捕获入口，不能当作周期彩色 GUI 队列或活动窗口关闭验证，也不是性能测量。

第三参数启用真实队列核验。使用独立 SettingPageModel（周期彩色开启）和原子进度存储创建三个真实 QueueTask／SubtitleTask，置入实际 SubtitlePage 队列并挂到 MainWindow。取消首项后第二项必须自动启动并更新预览；通过实际 SelectTask 切回首项；第二项运行时执行主窗口 Close，须完成原 DisposeTasksAsync 路径、保存第二项取消状态、保留首项检查点哈希、第三项不启动，所有控件处理器清空。两项检查点必须保留 PeriodicColor 捕获身份。公共控件构造仍使用用户设置和原队列持久化；新增内部设置注入只服务隔离运行。

证据 `.codex-build/periodic-queue-window-v2/qualification/report.json` 全部通过，实际原生库为 5B13...B984，真实用户存储未变。首次 `.codex-build/periodic-queue-window` 因探针缺失 DispatcherSynchronizationContext 导致跨线程集合修改后超时；修正为正常 WPF 的上下文后新目录运行成功，不据首次失败修改产品队列。窗口透明、不激活，不执行原 App 通知／单实例启动，也未核验可见视觉效果。设置导航来自真实 NavigationView；字幕页由探针挂载，跳过资源检查和真实进度恢复询问。预览开关断言针对先执行的普通捕获控件，周期彩色队列检查实际预览更新。

周期彩色队列预览开关补充证据 `.codex-build/periodic-queue-preview/qualification/report.json`：真实首项收到图像后关闭 ShowPreview，至少再处理 100 帧且图像对象不更新；重新开启后必须恢复更新且任务仍在运行。随后取消首项、自动启动第二项、切回首项及活动窗口关闭保存／释放检查全部通过，第三项未启动，用户存储未变。该版本报告 PeriodicQueuePreviewToggleQualified=true，补齐先前只有普通捕获控件执行预览开关断言的缺口。仍不证明可见窗口效果或性能收益。

工厂失败恢复检查随队列模式运行：周期 0／1000001 须抛 ArgumentOutOfRangeException，错误插件指纹须抛 InvalidDataException；三轮缺失视频须抛 InvalidOperationException，随后同工厂每轮均能读取有效视频的第 1～13 帧，通道周期正确，重复 Dispose 后指针为零。证据 `.codex-build/periodic-factory-failure-v2/qualification/report.json` 的 ProductFactoryFailureRecoveryQualified=true，后续真实队列及用户记录不变检查仍通过。首次探针错误地使用从 0 开始的周期索引，保留失败输出；修正为产品第 1 帧编号后通过。此检查不量化原生模块引用泄漏或任意错误恢复。
# 周期彩色可用性预检查

设置模型初始化时核验实际加载运行库和配套插件。原包、缺失／错误插件或无法安全选择的重复库使选项禁用；旧设置中的勾选自动清除并持久化，保留其他设置。共享设置实例改为延迟创建，隔离构造不触碰用户文件。

第二参数 `unavailable` 用于不支持模式验证：在隔离文件保存旧勾选，确认取消及落盘、拒绝重新启用、实际设置页复选框禁用。实际页面通过内部模型注入构造，公共入口仍使用共享设置。证据 `.codex-build/periodic-availability/unavailable/report.json` 和 `bad-plugin/report.json`；用户文件指纹未变。

合格扩展库证据 `.codex-build/periodic-availability/qualified/report.json`：复选框仍可用，已有设置绑定、预览开关、队列取消／推进及窗口关闭保存检查全部通过，实际库 EC795E...DB35D1。未验证可见视觉效果。已保存的周期彩色断点仍按原身份检查，禁用新任务选项不改变旧断点身份。

