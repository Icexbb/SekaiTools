# 单视频识别实验入口

## 应用内入口（实验性）

字幕页 → 添加任务 → 勾选底部“纯视频时间轴（实验性）” → 只选择视频 → 加入队列。应用使用 C# / OpenCV 实现，不依赖 Python、OCR 模型、剧本或翻译文件；复用已有菜单模板资源。

仅视频模式会隐藏剧本、翻译及同名文件自动匹配组件；切回普通模式后恢复。识别时支持画面预览，并响应“显示预览”开关。

完成后使用原有对白行的译文输入、快速编辑和时间轴调整功能，再通过原有“输出”按钮导出普通 ASS。角色未识别时显示“未指定角色”，原文为提示文字；这些提示不进入字幕。空译文条目不会导出。字幕样式使用当前对白字体和普通底部布局，不生成游戏遮罩或打字机动画；未知原文布局统一按 Line1 导出，受“单行”导出开关控制。

本轮支持停止/取消和任务移除；暂不支持纯视频任务的断点恢复、历史保存或跨会话保留手填译文，添加任务时已提示及时导出。最新精度修复已完成 4 组、169 条参考对白的时间对齐回归，包含此前未通过的 16:10 长视频，详见 [修复报告](../../docs/single-video-timing-accuracy-fix.md)。这不等同于人工逐帧真值验收，未知素材仍需复核；当前布局是比例预设，不支持任意裁切、黑边和变化中的对话框位置。

实际 C# 与离屏 WPF 验收工具在 [VideoTimingProbe](../VideoTimingProbe/README.md)。下文 Python 工具继续保留用于算法对照。

## 当前主目标：只识别时间轴

需求已确认：“字幕长度”指每条对白显示的起止时间和时长，不是字符数或文字宽度。基础模式只需要分出对白时间段，随后复用产品现有的逐条翻译编辑、时间轴调整和字幕导出方式手填译文；原文与角色名识别属于更高识别强度的可选目标，不能成为基础模式的前置条件。

新增 `timing.py` 是无 OCR 的视觉变化实验：只依赖 OpenCV/Numpy，不导入 RapidOCR/ONNX Runtime，不下载模型，不读取剧本、翻译或参考状态。根据文字像素的增加/清空/替换检测换句，原文和角色留空。默认约 12 次/秒取样，时间尚未逐帧精修。

```powershell
# 只需要基础时间轴模式时，安装 requirements-timing.txt 即可，无需 OCR 包。
python -m venv .codex-build/single-video/venv
.codex-build/single-video/venv/Scripts/python -m pip install -r Tools/SingleVideoProbe/requirements-timing.txt
.codex-build/single-video/venv/Scripts/python -X utf8 Tools/SingleVideoProbe/timing.py E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/single-video/my-timing
# 完成后独立比较时间段；不使用文本匹配，也不反向修改识别结果。
python -X utf8 Tools/SingleVideoProbe/compare_timing.py .codex-build/single-video/my-timing/recognition.json .codex-build/single-video/reference-short/state-1.json .codex-build/single-video/my-timing-comparison.json
```

输出 `recognition.json`（空 `translation`）和 `visual-samples.jsonl`，可使用下文 `evidence` 生成截图。实验阶段可在 JSON 中填入 `translation`，再用 `export` 导出；空译文不生成空白字幕。JSON 仅是 Python 实验接口，应用内入口直接识别视频并复用现有编辑器，不要求用户编辑 JSON，也暂不导入这些实验 JSON。

基础模式的评估重点是多余/缺失时间段、换句正确性、起止帧误差及编辑导出流程，不是 OCR 字符错误率。当前视觉门槛仍会将片头、片尾主界面和横幅误识别为对白，下一步优先补对话框存在检测，再做 GUI 接入。

## 可选高级目标：识别原文

只输入视频，用本地 OCR 提取角色名、日文对白和抽样时间轴，输出可编辑 JSON、SRT、基础 ASS。此工具是模型与算法验证入口，尚未接入 WPF 字幕任务，不调用现有依赖剧本的 `VideoProcessor`，不读写应用历史和检查点。

`recognize` 不接受剧本或翻译参数，也不搜索同名文件；`evaluate` 在识别完成后独立读取剧本进行顺序对齐，绝不回填、纠正识别结果。视频、剧本、翻译文件均只读。输出目录必须是新目录。

## 安装和运行

在仓库根目录执行，建议 Python 3.13 x64。本次验证使用 CPU ONNX Runtime，依赖版本记录在 `requirements.txt`；模型文件不提交 Git。

```powershell
python -m venv .codex-build/single-video/venv
.codex-build/single-video/venv/Scripts/python -m pip install -r Tools/SingleVideoProbe/requirements.txt
.codex-build/single-video/venv/Scripts/python -X utf8 Tools/SingleVideoProbe/probe.py recognize E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/single-video/my-run --models .codex-build/single-video/models
```

首次执行由 RapidOCR 下载选定模型到 `--models` 目录，之后已有模型可离线运行。需要联网安装和首次下载模型；视频帧始终在本机处理。模型下载地址由固定版本的 RapidOCR 管理，不是产品运行时配置。若以后纳入产品，需接入项目统一资源下载、校验及网络端点配置。

默认使用 PP-OCRv5 mobile 检测 + server 识别。`--recognizer japan-v4` 切换为 PP-OCRv4 日文 mobile 识别，用于模型对照；更轻的模型并不保证更准确。配置依据：[RapidOCR 模型列表](https://rapidai.github.io/RapidOCRDocs/main/en/model_list/)。

`--interval .25` 是默认抽样间隔，`--max-seconds 60` 可限制处理长度。`auto` 只按画面比例选择初始布局预设，不代表已实现自动对话框定位。`--layout tablet|wide` 可指定预设；`--roi X0 Y0 X1 Y1 SPLIT` 使用归一化坐标指定裁剪范围和名字/正文分界，例如 `--roi .10 .79 .90 .93 .84`。三行文字必须全部在裁剪范围内。

## 输出及校对

- `recognition.json`：对白、角色、起止时间、置信度、取证时刻、实际模型哈希、版本和识别参数。
- `samples.jsonl`：每次 OCR 原始结果。保留打字增长过程，便于检查错误分句。
- `subtitles.srt` / `subtitles.ass`：日文原文，角色名加在正文前。ASS 为普通底部字幕，不包含游戏遮罩、位置跟踪或打字机效果。

```powershell
# 生成逐句裁剪截图和本地 review.html，直接用浏览器打开 HTML 即可查看。
.codex-build/single-video/venv/Scripts/python -X utf8 Tools/SingleVideoProbe/probe.py evidence .codex-build/single-video/my-run/recognition.json
# 编辑 JSON 中的 dialogs 后重新导出，保留原始输出。
python -X utf8 Tools/SingleVideoProbe/probe.py export .codex-build/single-video/my-run/recognition.json .codex-build/single-video/corrected
# 调整分句算法后，复用原始样本，避免重跑模型；该命令会重新产生对白，覆盖手工校对的意义不成立。
python -X utf8 Tools/SingleVideoProbe/probe.py reassemble .codex-build/single-video/my-run/recognition.json .codex-build/single-video/regrouped
```

每条结果均标记 `needs_review=true`，OCR 分数不是正确率。校对页为只读截图列表，实际修改在 JSON 中完成。`evidence` 需要原视频仍在原位置。

## 与现有带剧本模式比较

```powershell
python -X utf8 Tools/SingleVideoProbe/probe.py inventory E:/ProjectSekai/test .codex-build/single-video/inventory.json
# 带剧本参考使用现有 RecognitionBenchmark，详见其 README。该工具要求同名 JSON 和 TXT。
dotnet run --project Tools/RecognitionBenchmark -c Release -- E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.mp4 .codex-build/single-video/reference 1 false Automatic false 4 false false
python -X utf8 Tools/SingleVideoProbe/probe.py evaluate .codex-build/single-video/my-run/recognition.json E:/ProjectSekai/test/1963_areatalk_ev_street_16_003.json .codex-build/single-video/comparison.json --state .codex-build/single-video/reference/state-1.json
```

`evaluate` 使用编辑距离的动态规划单调对齐，报告缺失、多余、整句准确数、角色一致数，以及字符错误率。正文先 NFKC 归一化并去除空白，保留标点；插入、删除均计入错误。对齐只是自动诊断，尤其在重复或相似台词中需要人工复核。不能把“有对齐配对”直接解释成正确识别。

`--state` 可比较带剧本结果的起止帧，要求视频路径一致、FrameSet 数等于完整剧本对白数。时间按帧号/FPS 计算，是与现有算法的差异，不能作为人工真值或可变帧率精度保证。`--max-seconds` 的部分识别若对完整剧本评估，未处理部分会计为缺失。

## 当前边界与后续接入

当前只处理有名字框的常规对白。不识别地点横幅、歌曲标记、无名字对白或画外语音；不提供机器翻译。没有角色名且没有已建立的对白跟踪时，会忽略 OCR 文本，以减少背景标牌误检。

分句利用名字变化、正文前缀增长、相似度、多次变化确认和时间间隔。两次变化确认会增加短对白漏检风险；没有可见间隔的相同台词、相同前缀的下一句仍可能合并。短暂清空若落在抽样间隔之间也可能漏掉。整个结果是候选字幕，需要校对。

下一步应先用对话框视觉状态检测替代名字存在门槛，增加打字重置证据和边界逐帧回查，再增加横幅检测。产品化时在 Core 定义视频独立事件、OCR 契约和处理结果，由 Infrastructure 承载模型推理/资源实现，GUI 增加实验性任务入口及校对界面；原有带剧本匹配流程继续作为独立模式。

## 验证

```powershell
python -m unittest discover -s Tools/SingleVideoProbe -v
```

这些测试验证打字合并、瞬时错误、连续同角色/间隔后重复对白、背景文本排除、ASS 转义及离线评估。真实视频实验与局限见 [实验分析](../../docs/single-video-recognition-analysis.md)。
