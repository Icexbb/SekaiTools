using Xunit;

namespace SekaiTools.UITests.Fixtures;

public sealed class DesktopFactAttribute : FactAttribute
{
    public DesktopFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SEKAI_TOOLS_RUN_UI_TESTS") != "1")
            Skip = "未执行：请使用 Tools/RunUITests.ps1 显式启用交互桌面测试。";
    }
}

[CollectionDefinition("Desktop", DisableParallelization = true)]
public sealed class DesktopCollection;
