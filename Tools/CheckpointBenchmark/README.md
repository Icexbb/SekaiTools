# 检查点存储规模基准

独立后端基准，不解码视频、不运行匹配器，也不访问用户进度或历史目录。用于区分 JSON 缩进、全量写入与增量分块的成本；不能将结果换算为完整识别提速。

```powershell
dotnet publish Tools/CheckpointBenchmark -c Release -o .codex-build/recognition-plan/checkpoint
foreach ($round in 1..4) {
    dotnet .codex-build/recognition-plan/checkpoint/CheckpointBenchmark.dll ".codex-build/recognition-plan/storage-policy-append/round-$round" $round
}
```

每次必须使用新的输出目录；第二个参数为 1–4，轮换四种模式的先后顺序。每次调用是独立进程，没有额外预热。

| 模式 | 路径 |
| --- | --- |
| legacy | 产品原 `ProgressStore.SaveToPath`，缩进全量 JSON |
| compact | 紧凑全量 JSON，调用与队列相同的原子写入及刷盘实现 |
| adaptive | 当前队列 `IncrementalProgressStore.Save` 的自适应选择 |
| chunked | 强制分块，用于隔离策略开销，不是产品设置 |

两类合成状态分别包含 4/64 个已完成事件，每个事件 4000 条轨迹，初始时间码 20000/300000 项。每种模式连续保存五次，每次追加 64 条活动轨迹和 64 个时间码。已完成事件 DTO 和完整时间码前缀块在五次保存间复用，尾块随时间码增长重新捕获；各模式重新建立自己的对象，避免跨模式复用身份缓存。

计时仅包围保存调用，不包含状态构造、加载及校验。每次保存后从磁盘加载并与完整预期状态比较，全部一致才生成报告。报告记录五次耗时、总耗时、文件总大小和物理载荷文件数（当前 v2 为包文件，可包含多个逻辑块）；文件总大小包含此前尾块。每种模式完成后仅调用产品删除入口清理本模式载荷，保留报告，失败时保留现场。

结果需分别观察首次写入、后四次追加和五次总成本。首次分块需要写入多个不可变文件，可能比全量更慢；不能只公布追加收益。全量输入已在计时前构造，不包含将冻结时间码展开为列表的成本。模板和视频不参与本工具。正式记录应额外保存工具、Core、Infrastructure 和 runtimeconfig 的运行前后摘要；测量期间不要构建或运行其他性能任务。
