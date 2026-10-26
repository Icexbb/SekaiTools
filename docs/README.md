# 文档索引

首次使用请先阅读项目根目录的 [使用说明](../README.md)。项目边界见 [架构说明](../ARCHITECTURE.md)，构建与协作约定见 [CLAUDE.md](../CLAUDE.md)。

## 使用与功能规则

| 文档 | 内容 |
| --- | --- |
| [地图对话筛选](action-story-filtering.md) | 下载筛选、活动关联、追加推断与缺失数据降级 |
| [翻译编辑](translation.md) | 普通与参考翻译、闪回来源、保存标签与覆盖确认 |
| [翻译 TXT 格式 v2](translation-format-v2.md) | 标签、编号、转义、空正文及旧格式兼容 |
| [字幕任务与复核](subtitle-workflow.md) | 一次性恢复、历史按需加载、时间轴定位及输出保护 |
| [字幕识别规则](recognition.md) | 匹配状态机、无名对话、短暂失配容错与识别边界 |
| [压制性能与编码并行度](video-suppression-performance.md) | 软件编码线程选择、性能测量与取消行为 |
| [小窗口与 DPI 适配](window-layout.md) | 页面断点、窗口边界和实际设备验收 |

## 配置与开发

| 文档 | 内容 |
| --- | --- |
| [翻译检查配置](translation-rules.md) | `translation-rules.json` 的正则替换与反馈 |
| [设置页界面配置](settings-ui-configuration.md) | `settings-ui.json` 的控件、绑定、图标及还原范围 |
| [识别性能与诊断](recognition-performance.md) | 帧池、内存、实验取舍、复测工具及文件选择延迟排查 |
| [macOS 首版移植](macos-port.md) | 已确认范围、Host 协议、原生依赖门槛及未完成验收 |
| [Git 提交与版本发布规范](git-and-version-conventions.md) | 提交格式、版本同步、发布说明和发布检查 |

## 维护方式

文档以功能规则、配置协议和可重复操作为主；同一规则维护在一个文档，其他位置使用链接。功能实现变化时同步修正文档。

逐次实验日志、构建过程、原型截图、聊天记录和本机临时路径不作为正式说明长期累积。工具使用方法放在对应 `Tools/*/README.md`，原始证据放在 Git 忽略的本地目录；正式文档只保留测量条件、最终取舍与验证边界。历史提交和发布说明用于追溯旧实现。

历史性能结果不代表当前构建或其他设备的表现；自动测试、组件渲染与实际设备人工验收分别记录，不相互替代。仓库中的文档目录统一使用小写 `docs/`，保证链接在大小写敏感环境中有效。
