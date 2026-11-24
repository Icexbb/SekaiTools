# 开发者模式与 HTTP API

在“设置与关于”的 Sekai Tools 信息区连续点击版本号 7 次启用，相邻点击间隔不超过 3 秒。版本号也支持聚焦后按 Enter 或空格。开发者设置默认折叠，启用后手动展开。可点击分类末尾的“关闭开发者模式”停用。默认关闭，开关保存在普通设置中。启用后在“开发者设置 → HTTP API”显示监听地址和令牌掩码，可分别点击复制按钮获取地址和完整令牌。关闭模式或退出程序会停止监听，再次启用会生成新令牌。正式运行固定监听 `http://127.0.0.1:18339/`，端口被占用时 HTTP API 面板显示错误，可关闭后重试。

开启后设置页会立即显示“开发者设置”分类，可编辑六类识别阈值，点击“保存阈值”应用并持久化，或点击“恢复默认来源”清除覆盖。关闭开发者模式会立即隐藏该分类。界面与 HTTP API 使用同一份阈值；API 修改后已打开的编辑器会刷新。保存前校验所有输入，失败时保留原有参数并显示错误。

所有请求必须携带 `Authorization: Bearer <从 HTTP API 面板复制的令牌>`。接口仅供本机脚本调用，拒绝包含 Origin 的浏览器请求。请求体为 JSON，最多 16 KiB。不要把令牌放进日志或版本库。

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/v1/status` | 版本、进程号、阈值覆盖状态 |
| GET | `/api/v1/thresholds` | 当前有效的六类识别阈值 |
| PATCH | `/api/v1/thresholds` | 部分修改阈值，字段名区分大小写 |
| DELETE | `/api/v1/thresholds` | 删除开发者覆盖，恢复资源文件或内置阈值 |
| GET | `/api/v1/settings` | 查询可调整的识别设置 |
| PATCH | `/api/v1/settings` | 部分修改下表中的识别设置 |
| POST | `/api/v1/window/activate` | 显示、还原并激活主窗口 |
| POST | `/api/v1/navigation` | 切换页面，正文例如 `{"page":"settings"}` |

页面名：`subtitle`、`translate`、`download`、`suppress`、`settings`。页面导航沿用界面原有的初始化流程。

阈值字段：`DialogNametagNormal`、`DialogNametagSpecial`、`DialogContentNormal`、`DialogContentSpecial`、`BannerNormal`、`MarkerNormal`。取值必须是大于 0、不超过 1 的有限数字。开发者阈值保存到应用数据目录的 `Data/developer-thresholds.json`，只有启用开发者模式时才使用；关闭后保留该文件以便下次调试。没有覆盖时沿用原有 `VideoProcess/thresholds.json` 资源及内置默认值。

| 识别设置 | 取值 |
| --- | --- |
| RecognitionPerformanceMode | 0～2，与设置页模式选项顺序一致 |
| RecognitionMemoryBudgetMiB | 128～4096，整数 |
| RecognitionFrameBufferChoice | 0 自动、1 四帧、2 八帧、3 十六帧 |
| RecognitionPrepareGray | 布尔值；开启时关闭周期彩色，与设置页一致 |

PATCH 会先校验全部字段，再修改；未知字段和非法参数返回 400，鉴权失败返回 401，未知路径或方法返回 404。修改在 UI 线程执行，与设置页同步。识别设置保存到普通设置文件，关闭开发者模式后仍保留。阈值及识别设置只影响之后启动的处理器，正在运行的任务和既有识别历史不会重新计算；断点恢复仍受原有配置兼容校验约束。

PowerShell 示例：

```powershell
$headers = @{ Authorization = 'Bearer 替换为从面板复制的令牌' }
$api = 'http://127.0.0.1:18339/api/v1'
Invoke-RestMethod "$api/status" -Headers $headers
Invoke-RestMethod "$api/thresholds" -Method Patch -Headers $headers `
  -ContentType 'application/json' -Body '{"DialogContentNormal":0.85,"BannerNormal":0.8}'
Invoke-RestMethod "$api/settings" -Method Patch -Headers $headers `
  -ContentType 'application/json' -Body '{"RecognitionMemoryBudgetMiB":1024}'
Invoke-RestMethod "$api/window/activate" -Method Post -Headers $headers
Invoke-RestMethod "$api/thresholds" -Method Delete -Headers $headers
```
