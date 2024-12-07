namespace SekaiToolsCore.Process;

public static class ResourceManager
{
    private const string BasePath = "Resource";

    public static string ResourcePath(string fileName)
    {
        string[] baseDirs =
        [
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
            AppDomain.CurrentDomain.BaseDirectory,
            AppDomain.CurrentDomain.RelativeSearchPath ?? string.Empty,
            AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty
        ];
        baseDirs = baseDirs.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        foreach (var baseDir in baseDirs)
        {
            var filename = Path.Combine(baseDir, BasePath, fileName);
            if (File.Exists(filename)) return filename;
        }

        throw new FileNotFoundException($"{Path.Combine(BasePath, fileName)} not found");
    }
}