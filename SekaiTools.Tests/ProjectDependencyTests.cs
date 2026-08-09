using System.Xml.Linq;

namespace SekaiTools.Tests;

public class ProjectDependencyTests
{
    public static TheoryData<string, string[]> AllowedDependencies => new()
    {
        { "SekaiToolsBase", [] },
        { "SekaiToolsConfiguration", [] },
        { "SekaiToolsSubtitles", [] },
        { "SekaiToolsMedia", [] },
        { "SekaiDataFetch", ["SekaiToolsBase", "SekaiToolsConfiguration"] },
        { "SekaiToolsCore", ["SekaiToolsBase", "SekaiToolsSubtitles"] },
        { "SekaiToolsInfrastructure", ["SekaiToolsBase", "SekaiToolsConfiguration", "SekaiToolsCore", "SekaiToolsMedia"] },
        {
            "SekaiToolsGUI",
            ["SekaiDataFetch", "SekaiToolsConfiguration", "SekaiToolsCore", "SekaiToolsInfrastructure", "SekaiToolsMedia", "SekaiToolsSubtitles"]
        },
        { "Updater", ["SekaiToolsConfiguration"] }
    };

    [Theory]
    [MemberData(nameof(AllowedDependencies))]
    public void 产品项目只引用允许的直接依赖(string projectName, string[] expectedDependencies)
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, projectName, $"{projectName}.csproj");
        var document = XDocument.Load(projectPath);
        var actualDependencies = document.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedDependencies.Order(StringComparer.OrdinalIgnoreCase), actualDependencies);
    }

    [Fact]
    public void GUI发布目标会显式还原并发布Updater()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(root, "SekaiToolsGUI", "SekaiToolsGUI.csproj");
        var document = XDocument.Load(projectPath);
        var buildUpdater = document.Descendants("Target")
            .Single(element => element.Attribute("Name")?.Value == "BuildUpdater");
        var updaterBuild = buildUpdater.Descendants("MSBuild")
            .Single(element => element.Attribute("Projects")?.Value.Contains("Updater.csproj",
                StringComparison.OrdinalIgnoreCase) == true);
        var targets = updaterBuild.Attribute("Targets")?.Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        Assert.Contains("Restore", targets);
        Assert.Contains("Publish", targets);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SekaiTools.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法从测试输出目录定位 SekaiTools.sln");
    }
}
