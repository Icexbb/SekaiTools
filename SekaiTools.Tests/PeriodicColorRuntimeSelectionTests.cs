using SekaiToolsInfrastructure.Video;

namespace SekaiTools.Tests;

public class PeriodicColorRuntimeSelectionTests
{
    [Fact]
    public void DuplicateOriginalLibrariesExplainUnsupportedExperiment()
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            PeriodicColorCaptureFactory.SelectQualifiedRuntimePath(["root/cvextern.dll", "runtimes/cvextern.dll"], _ => "original"));
        Assert.Contains("请关闭该实验设置", exception.Message);
    }

    [Theory]
    [InlineData(PeriodicColorCaptureFactory.QualifiedNativeHash)]
    [InlineData(PeriodicColorCaptureFactory.QualifiedExpandedNativeHash)]
    public void SingleQualifiedLibraryIsAccepted(string fingerprint)
    {
        Assert.Equal("candidate/cvextern.dll", PeriodicColorCaptureFactory.SelectQualifiedRuntimePath(
            ["candidate/cvextern.dll"], _ => fingerprint));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MultipleDistinctLibrariesCannotSelectAnArbitraryQualifiedInstance(bool sameBytes)
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            PeriodicColorCaptureFactory.SelectQualifiedRuntimePath(["root/cvextern.dll", "runtimes/cvextern.dll"],
                path => sameBytes || path.StartsWith("root") ? PeriodicColorCaptureFactory.QualifiedExpandedNativeHash : "original"));
        Assert.Contains("加载了多份", exception.Message);
        Assert.Contains("root/cvextern.dll", exception.Message);
        Assert.Contains("runtimes/cvextern.dll", exception.Message);
    }

    [Fact]
    public void RepeatedEnumerationOfSamePathDoesNotMeanTwoInstances()
    {
        Assert.Equal("candidate/cvextern.dll", PeriodicColorCaptureFactory.SelectQualifiedRuntimePath(
            ["candidate/cvextern.dll", "CANDIDATE/CVEXTERN.DLL"], _ => PeriodicColorCaptureFactory.QualifiedExpandedNativeHash));
    }
}
