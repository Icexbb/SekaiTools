using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionOutputAuthorizationTests
{
    [Fact]
    public void EncodingUsesUniqueSystemTempFilesUntilCommit()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "output.mp4");
            using var first = new VideoOutputTransaction(target, false);
            using var second = new VideoOutputTransaction(target, false);
            Assert.Equal(Path.Combine(Path.GetTempPath(), "SekaiTools", "Suppression"),
                Path.GetDirectoryName(first.TemporaryPath));
            Assert.NotEqual(first.TemporaryPath, second.TemporaryPath);
            Assert.Equal(".mp4", Path.GetExtension(first.TemporaryPath));

            File.WriteAllText(first.TemporaryPath, "completed video");
            Assert.Empty(Directory.GetFiles(directory));
            first.Commit();

            Assert.Equal("completed video", File.ReadAllText(target));
            Assert.False(File.Exists(first.TemporaryPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void FileAppearingBeforeCommitIsNotReplacedWithoutAuthorization()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var target = Path.Combine(directory, "output.mp4");
            using (var transaction = new VideoOutputTransaction(target, false))
            {
                File.WriteAllText(transaction.TemporaryPath, "new output");
                File.WriteAllText(target, "external output");
                Assert.Throws<IOException>(() => transaction.Commit());
                Assert.Equal("external output", File.ReadAllText(target));
            }

            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
