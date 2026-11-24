# M6 设置与关于验收

## 构建与基线

- 交接提交 `e461ce5` 的工作区干净。首次基线运行因 VSTest 与 testhost 连接超时执行了 0 项。随后从该提交重新导出源码，在交互式桌面补跑，原有主题与开发者阈值用例 2 项均通过。旧版会显示完整令牌，基线副本停用了测试截图，应用源码与功能断言保持原样。
- Release GUI 和 FlaUI 项目构建通过。构建仍报告 Emgu 包兼容性警告和 UI 测试项目的 System.Drawing.Common 引用警告。
- Release 单元测试 674 项全部通过。
- 基线 TRX 位于 `.codex-build/m6-baseline/.codex-build/fluent-workbench/artifacts/20261124-175446-aef008c6/`。最终 FlaUI 运行 5 项全部通过，TRX、分步报告与截图位于 `.codex-build/fluent-workbench/artifacts/20261124-175708-abbaf20b/`。

## 功能与 UIA

| 用例 | 验收内容 |
| --- | --- |
| UI-SETTINGS-001 | 主题自动保存及跨进程重载。取消全局还原保持设置文件哈希，确认后恢复默认主题 |
| UI-SETTINGS-002 | 版本号激活、开发者分类位置与显隐、隔离端口监听与关闭、连接地址、令牌掩码、未认证请求返回 401、阈值保存与重载、非法值反馈及恢复阈值来源 |
| UI-SETTINGS-003 | 代理、下载目录、下载源模式、译文换行标记、打字机字符间隔、字幕字体和灰度预处理的保存及跨进程重载。检查单项及全局还原，并验证关于与运行日志入口 |
| UI-SETTINGS-004 | 亮色、暗色和内置高对比度主题下的最小窗口、代理编辑器、底栏可见性及分类顺序 |
| UI-SETTINGS-005 | 回环服务器返回新版本，显示确认提示，取消时只发生版本查询 |

| 标识 | 控件与 Pattern |
| --- | --- |
| `Settings.Field.{Property}` | choice 使用 ComboBox 的 ExpandCollapse 与选项的 SelectionItem。text、number、directory 和 font 使用 Value。check 使用 Toggle |
| `Settings.Choose.{Property}` | directory 和 font 的选择按钮使用 Invoke |
| `Settings.Reset.{Property}`、`Settings.ResetSection.{Property}` | 卡片与分组还原按钮使用 Invoke，标识采用首个所属属性 |
| `Settings.Section.{Property}` | 嵌套分组使用 ExpandCollapse，标识采用分组内首个所属属性 |
| `Settings.About`、`Settings.Repository`、`Settings.RuntimeLog` | 关于卡片使用 ExpandCollapse，仓库链接可定位，运行日志入口使用 Invoke |
| `Settings.Developer.Address`、`Settings.Developer.Token` | 两个只读 Value 输入，令牌输入仅返回掩码 |

内置配置没有使用生成器的 `toggle` 分支。周期彩色读取依赖专用实验运行库，灰度预处理用于验证当前环境中可操作的实验复选框。

## 截图复核

在 96 DPI、2560×1440 的 Windows 交互式桌面查看了三种应用主题的 960×600 截图。代理三列、主题编辑器和固定底栏均可见。首轮发现暗色自动保存提示难以辨读，改用主题前景资源后复核通过。正常滚动画面允许视口边缘显示部分下一张卡片。亮色开发者面板在 960×600 的截图中显示完整的地址与令牌掩码，复制按钮和底栏可见。

## 未实测

真实升级的下载、校验、安装和回滚属于独立集成验收。未打开外部仓库页面，目录与字体选择按钮核对到 Invoke，实际值通过编辑器修改。未验证系统级高对比度、125% 或 150% 缩放及专用运行库下的周期彩色读取。
