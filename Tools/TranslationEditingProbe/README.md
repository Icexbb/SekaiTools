# 翻译编辑回归探针

Windows / .NET 10：

```powershell
dotnet run --project Tools/TranslationEditingProbe/TranslationEditingProbe.csproj -p:UseSharedCompilation=false
```

覆盖正文修改、保存基线前后的撤销/重做、角色名及地点联动撤销、新编辑清除重做分支、500 步上限、显式空译文草稿恢复、v2 导出效果编号、损坏草稿容错、清空历史和主要模块恢复（含设置排除及无效值回退）。同时验证真实 WPF 按钮绑定、文本框撤销命令、页面卸载刷新草稿，以及明暗主题下 960/1280 宽度的按钮布局。使用随机临时目录，不读写真实用户的会话文件。

运行通过不等同于原生确认弹窗、真实键盘输入法和实际系统 DPI 的人工验收。
