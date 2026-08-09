using System.Xml.Linq;

namespace SekaiTools.Tests;

public class ProjectDependencyTests
{
    public static TheoryData<string, string[]> AllowedDependencies => new()
    {
        { "SekaiToolsBase", [] },
        { "SekaiToolsSubtitles", [] },
        { "SekaiToolsMedia", [] },
        { "SekaiDataFetch", ["SekaiToolsBase"] },
        { "SekaiToolsCore", ["SekaiToolsBase", "SekaiToolsSubtitles"] },
        { "SekaiToolsInfrastructure", ["SekaiToolsBase", "SekaiToolsCore", "SekaiToolsMedia"] },
        {
            "SekaiToolsGUI",
            ["SekaiDataFetch", "SekaiToolsCore", "SekaiToolsInfrastructure", "SekaiToolsMedia", "SekaiToolsSubtitles"]
        },
        { "Updater", [] }
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
