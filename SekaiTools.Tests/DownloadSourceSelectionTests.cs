using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel.Download;

namespace SekaiTools.Tests;

public class DownloadSourceSelectionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidSelectionUsesFirstSource(int index)
    {
        var model = new DownloadPageModel { SourceData = SourceData.Default };
        model.CurrentSourceIndex = index;
        Assert.Equal(0, model.CurrentSourceIndex);
        Assert.Same(model.SourceData[0], model.CurrentSource);
    }

    [Fact]
    public void ValidSelectionUsesChosenSource()
    {
        var model = new DownloadPageModel { SourceData = SourceData.Default };
        model.CurrentSourceIndex = model.SourceData.Length - 1;
        Assert.Same(model.SourceData[^1], model.CurrentSource);
    }

    [Fact]
    public void ReplacingSourceListHandlesTransientDeselectionAndSelectsFirst()
    {
        var model = new DownloadPageModel { SourceData = SourceData.Default };
        model.CurrentSourceIndex = model.SourceData.Length - 1;
        var replacement = new[] { model.SourceData[0] };
        var selectionNotified = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(model.SourceData))
            {
                // WPF clears selection during an ItemsSource replacement.
                Assert.Same(replacement[0], model.CurrentSource);
                model.CurrentSourceIndex = -1;
                Assert.Same(replacement[0], model.CurrentSource);
            }
            if (args.PropertyName == nameof(model.CurrentSourceIndex))
                selectionNotified = true;
        };

        model.SourceData = replacement;
        Assert.Equal(0, model.CurrentSourceIndex);
        Assert.Same(replacement[0], model.CurrentSource);
        Assert.True(selectionNotified);
    }

    [Fact]
    public void ReplacingSourcesAtIndexZeroStillNotifiesSelection()
    {
        var model = new DownloadPageModel();
        model.CurrentSourceIndex = 0;
        var notified = false;
        model.PropertyChanged += (_, args) =>
            notified |= args.PropertyName == nameof(model.CurrentSourceIndex);
        model.SourceData = [model.SourceData[0]];
        Assert.True(notified);
        Assert.Same(model.SourceData[0], model.CurrentSource);
    }

    [Fact]
    public void EmptySourceListFallsBackToDefaults()
    {
        var model = new DownloadPageModel { SourceData = [] };
        Assert.NotEmpty(model.SourceData);
        Assert.Same(model.SourceData[0], model.CurrentSource);
    }
}
