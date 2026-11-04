using SekaiToolsMedia;

namespace SekaiTools.Tests;

public class SuppressionStorageCheckTests
{
    private const long GiB = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(0, 2 * GiB)]
    [InlineData(3 * GiB, 7 * GiB)]
    [InlineData(long.MaxValue, long.MaxValue)]
    public void EstimateIncludesMinimumAndReserveWithoutOverflow(long source, long expected)
    {
        Assert.Equal(expected, SuppressionStorageCheck.EstimateRecommendedBytes(source));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0L, true)]
    [InlineData(2 * GiB - 1, true)]
    [InlineData(2 * GiB, false)]
    [InlineData(3 * GiB, false)]
    public void WarnsForLowOrUnknownFreeSpace(long? available, bool expected)
    {
        var check = new SuppressionStorageCheck("temp", available, 2 * GiB);
        Assert.Equal(expected, check.NeedsWarning);
        Assert.Contains("temp", check.WarningMessage);
    }
}
