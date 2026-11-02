using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionOutputAuthorizationTests
{
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
        finally { Directory.Delete(directory, true); }
    }
}
