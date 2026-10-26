# 翻译检查配置

运行目录中的 `translation-rules.json` 控制翻译编辑页的文本检查、自动替换及行长提示。构建和发布时自动复制；修改后重启程序生效。文件缺失时使用程序集内置的同一份默认配置。JSON 格式或字段值无效时会报告配置错误，不应删除必填字段。

| 字段 | 用途 |
| --- | --- |
| `replacements` | 按数组顺序执行自动替换，`from` 为原文，`to` 为替换结果；空数组关闭自动替换。不要配置循环或不断增长的替换规则 |
| `checkEndPunctuation` | 是否检查句尾标点 |
| `validEndCharacters` | 合理结束符号，每个字符独立；默认包含中文问号和感叹号 |
| `closingCharacters` | 检查句尾前忽略的闭合引号、括号，支持连续多个 |
| `missingEndMessage` | 缺少结束标点时的提示文字 |
| `checkEllipsisPunctuation` | 是否检查省略号后的多余标点 |
| `invalidEndings` | 不合理的结尾字符串列表；在自动替换后检查 |
| `invalidEndingMessage` | 命中不合理结尾时的提示文字 |
| `checkDash` | 是否检查破折号分组 |
| `dashCharacter` / `dashGroupLength` | 破折号字符及每组长度；默认 `—` 和 `2`。保留原规则：只有出现完整分组时，才检查是否混有不完整分组 |
| `dashMessage` | 破折号使用错误的提示文字 |
| `checkLineLength` | 是否显示过长提示 |
| `maxLineLength` | 普通台词的行长上限，默认 37 |
| `threeLineMaxLength` | 原文为三行时的行长上限，默认 45 |

例如，允许 `♡` 结尾时，将其追加到 `validEndCharacters`。关闭句尾检查时，将 `checkEndPunctuation` 改为 `false`。关闭英文问号自动转换时，从 `replacements` 删除对应条目，并将 `?` 加入 `validEndCharacters`。

行长继续沿用编辑器原有的加权计数方式。此配置用于文本提示，不改变剧本与翻译的条目／编号匹配校验，也不改变字幕导出的自动分句规则。
