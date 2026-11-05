using Updater;

namespace SekaiTools.Tests;

public sealed class UpdateInstallationTests
{
    [Theory]
    [InlineData(FileShare.Read)]
    [InlineData(FileShare.None)]
    public async Task ReplacementWaitsUntilDestinationIsReleased(FileShare sharing)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "new.dll");
        var destination = Path.Combine(root, "installed.dll");
        File.WriteAllText(source, "new version");
        File.WriteAllText(destination, "old version");
        try
        {
            using var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, sharing);
            var replacement = Task.Run(() => UpdateInstallation.Replace(source, destination));
            await Task.Delay(500);
            var waitedForRelease = !replacement.IsCompleted;
            locked.Dispose();
            await replacement;
            Assert.True(waitedForRelease);
            Assert.Equal("new version", File.ReadAllText(destination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
