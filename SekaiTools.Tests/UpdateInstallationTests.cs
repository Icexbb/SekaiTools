using Updater;

namespace SekaiTools.Tests;

public sealed class UpdateInstallationTests
{
    [Fact]
    public void FailedStartupRestoresManagedFilesAndPreservesUnrelatedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "install");
        var work = Path.Combine(root, "work");
        var stage = Path.Combine(root, "stage");
        var source = Path.Combine(stage, "SekaiTools");

        try
        {
            Directory.CreateDirectory(install);
            Directory.CreateDirectory(work);
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(install, UpdatePaths.MainExecutable), "old executable");
            File.WriteAllText(Path.Combine(install, "obsolete.dll"), "old library");
            File.WriteAllText(Path.Combine(install, "user.txt"), "user data");
            File.WriteAllLines(Path.Combine(install, UpdatePaths.Manifest),
                [UpdatePaths.MainExecutable, "obsolete.dll"]);
            File.WriteAllText(Path.Combine(source, UpdatePaths.MainExecutable), "invalid executable");
            File.WriteAllText(Path.Combine(source, "new.dll"), "new library");
            File.WriteAllLines(Path.Combine(source, UpdatePaths.Manifest),
                [UpdatePaths.MainExecutable, "new.dll"]);

            Assert.ThrowsAny<Exception>(() => UpdateInstallation.Run(install, work, stage, 0, 0));

            Assert.Equal("old executable", File.ReadAllText(Path.Combine(install, UpdatePaths.MainExecutable)));
            Assert.Equal("old library", File.ReadAllText(Path.Combine(install, "obsolete.dll")));
            Assert.Equal("user data", File.ReadAllText(Path.Combine(install, "user.txt")));
            Assert.False(File.Exists(Path.Combine(install, "new.dll")));
            Assert.False(File.Exists(Path.Combine(work, UpdatePaths.Journal)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
