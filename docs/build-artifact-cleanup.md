# 自动构建产物清理

入口：`Tools/CleanBuildArtifacts.ps1`。默认预览，加 `-Apply` 才执行删除。默认参数为 `-KeepDays 7 -ProtectHours 24 -MaxGiB 20`。

## 手动清理

双击仓库根目录 `clean-build.bat`，或执行 `pwsh -NoProfile -File Tools/CleanBuildArtifacts.ps1 -Interactive`。脚本先显示预览，输入大写 `YES` 才重新检查并执行清理，直接回车取消；没有候选或有活动构建时不会要求确认。需要 PowerShell 7。

命令行非交互方式仍可使用：不传开关只预览，传 `-Apply` 执行；可用 `-KeepDays`、`-ProtectHours`、`-MaxGiB` 调整保留周期和容量目标，保护规则不受影响。

- 只扫描脚本所属仓库的 `.codex-build/`，不跟随目录联接或符号链接；发现受跟踪文件或无法检查进程时停止。
- 按 `.codex-build` 下的一级目录分组，以组内最新文件更新时间判断近期使用，避免运行库原始时间戳较旧而误清近期构建。
- 白名单扩展名为 `.dll`、`.exe`、`.pdb`、`.lib`、`.obj`、`.so`、`.dylib`、`.ilk`、`.pch`。不会递归删除目录，也不会删除其他扩展名文件。
- 超过 7 天的候选清理；总占用超过 20 GiB 时按组的新旧继续选择候选，但始终保留最近 24 小时有更新的组。
- 任意子目录含 `.keep-build` 时保护整个一级目录组。`native` 和 `native-dependencies-pinned` 默认保护，以免丢失原生实验的唯一副本。根目录含 `.keep-build` 时整个清理停止。
- 活动构建／实验进程存在时跳过；删除前再次核对路径、重解析点、保护标记、文件大小和更新时间，锁定文件跳过并报告。
- 保留报告、JSON、ASS、日志、图片、视频、源码；`Build/`、`artifacts/`、项目 `bin/obj` 和 Git 数据均不在自动清理范围内。

容量限制是清理目标，不会越过保护项强行达到目标。输出报告包含清理前占用、候选量、实际释放量和失败文件数。需要复现实验时重新构建其二进制，保留的报告本身不代表原二进制仍存在。

已在 Codex 中为当前聊天设置每日香港时间 03:00 的检查，先预览再执行；无候选或正在构建时保持安静，仅实际清理、失败或需要处理时通知。调度受桌面应用和本机可用性限制，参见 [定时任务说明](https://learn.chatgpt.com/docs/automations)。该任务不恢复已暂停的识别优化目标。
