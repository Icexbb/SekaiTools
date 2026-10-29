# 识别流程本轮执行与验收（2026-10-29）

范围为已按顺序推进的六项识别改进，以及随后要求的自动构建产物定时清理、手动清理入口。正文按字逐个出现；不要求瞬时整句替换录像。该记录核对本轮交付，不代表 2.0 发布、所有素材准确率或稳定整链提速已经达成。

| 执行项 | 当前交付 | 核对证据 |
| --- | --- | --- |
| 1. 识别细分统计 | 按模板用途、调用状态记录请求、缓存命中与算子耗时，覆盖前瞻复核 | 当前统计入口及回归；早期对照记录 `.codex-build/recognition-plan/results.md`，其身份清单系事后补记，保留这一限制 |
| 2. 搜索位置提示评估 | 两份素材三轮收益方向不同，保持默认关闭 | `results.md` 记录短片约快 0.58%、活动片约慢 0.46%，不接受为稳定提速方案 |
| 3. 粗搜候选复核 | 首候选精修失败时最多再复核两个空间去重候选，附加分数门槛 | 干扰峰及弱候选回归；真实完整状态和 ASS 对照记录，算子次数增加约 4.3%，不宣称提速 |
| 4. 失配与回退 | 用实际帧间隔累计失败；短暂丢失保留阶段与截止帧，不补造轨迹；覆盖有名、无名、横幅、标记 | `DialogTrackingGraceTests`、`TrackingLossWindowTests`、`UnnamedDialogTests`；逐字重置与两字获取阶段磁盘恢复回归 |
| 5. 跨事件与后台调度 | 对话稳定时独立探测横幅／标记；前瞻按视觉变化唤醒并退避；慢写入只保留在途和最新待写快照 | `IndependentEventProbe`、`FiniteLookaheadTrackerTests`、`CoalescingProgressWriterTests`，完整状态及恢复对照 |
| 6. 增量检查点 | 小状态全量、较大状态 v2 包；已完成 DTO 和冻结时间码块复用；旧格式兼容、摘要和原子提交校验 | `IncrementalProgressStoreTests`、`IncrementalProgressPackTests`；默认 v2 长素材第 60000 帧取消恢复及三轮整链对照 |
| 构建产物清理 | 每日香港时间 03:00 清理；手动 `clean-build.bat` 或 PowerShell 预览／确认；保护近期和活动构建 | 自动化 `sekaitools` 已从 PAUSED 更新为 ACTIVE 并回读确认；当前手动预览为 `SkippedActiveProcesses`，未删除文件 |

最新验证：`dotnet format` 范围格式化及无变更检查通过，Release 全部 484 项非界面测试通过，Release 解决方案构建 0 错误，仍有既有依赖兼容性警告。日志保留于 `.codex-build/recognition-plan/typewriter-*.log`。没有据此宣称完成真实窗口、DPI 或全部视频验收。

默认 v2 长素材恢复：`long-default-v2-resume/verification.json` 验证取消点为 `chunks2:2.0`、精确从第 60000 帧恢复、输入／模板／运行文件身份未变；`byte-verification.json` 验证最终完整状态和 ASS 逐字节相同。三轮六次整链输出同样逐字节相同，证据位于 `long-default-v2-banner-cache-comparison/verification.json` 和 `summary.json`。

速度结论：本轮存储模式三轮配对耗时降幅分别为 7.15%、-0.33%、0.37%，方向有波动；保存累计耗时中位数由 284.80 ms 增至 404.03 ms。因此交付包含减少快照复制、约束内存与队列增长、减少大状态重复写入，但不把这些结构性改动等同稳定整链提速。无需为了获得正向数字而继续重复相同实验；后续速度工作应先选定新的可测瓶颈和验收指标，再另立实验。

真实逐字换句：WL 三组边界先重置为单字，走正常失配结束与重新获取。它证明该素材的兼容性，不证明独立区分复核路径在实录中命中；该保护路径由合成回归及磁盘恢复覆盖。根据用户确认，这一证据边界不再构成寻找瞬时替换素材的待办。

详见 [识别规则](recognition.md)、[性能结果](recognition-performance.md)、[检查点协议与恢复](checkpoint-storage.md)、[构建产物清理](build-artifact-cleanup.md)。定时执行仍依赖桌面应用及本机可用性，已启用不等于未来每次都已执行。未进行版本发布。
