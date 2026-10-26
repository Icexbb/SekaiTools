using SekaiToolsGUI.ViewModel.Subtitle;

namespace SekaiTools.Tests;

public class ExportSaveOptionsTests
{
    [Theory]
    [InlineData("event.txt", "翻译", "【翻译】event.txt")]
    [InlineData("【翻译】event.txt", "校对", "【校对】event.txt")]
    [InlineData("【校对】event.txt", "合意", "【合意】event.txt")]
    [InlineData("【合意】event.txt", "合意", "【合意】event.txt")]
    [InlineData("【旧标签】event.txt", "翻译", "【翻译】event.txt")]
    [InlineData("event【翻译】.txt", "校对", "【校对】event【翻译】.txt")]
    public void TagOnlyReplacesLeadingFilenameLabel(string name, string tag, string expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "【目录标签】");
        var model = new SaveFileDialogModel { FileName = Path.Combine(folder, name) };
        model.ApplyTranslationTag(tag);
        Assert.Equal(Path.Combine(folder, expected), model.FileName);
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".ass")]
    public void ExistingFileRequiresConsentUnlessPickerConfirmedThatPath(string extension)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "event" + extension);
            var other = Path.Combine(folder, "other" + extension);
            var model = new SaveFileDialogModel { FileName = path };
            Assert.False(model.NeedsOverwriteConfirmation);
            File.WriteAllText(path, "original");
            File.WriteAllText(other, "other");
            Assert.True(model.NeedsOverwriteConfirmation);
            model.SetSelectedPath(path, overwriteConfirmed: false);
            Assert.True(model.NeedsOverwriteConfirmation);
            model.SetSelectedPath(path, overwriteConfirmed: true);
            Assert.False(model.NeedsOverwriteConfirmation);
            model.FileName = other;
            Assert.True(model.NeedsOverwriteConfirmation);
            model.FileName = path;
            Assert.True(model.NeedsOverwriteConfirmation);
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData("翻译")]
    [InlineData("校对")]
    [InlineData("合意")]
    public void TagClickRevokesConsentEvenWhenSelectingCurrentTag(string tag)
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, $"【{tag}】event.txt");
            File.WriteAllText(path, "original");
            var model = new SaveFileDialogModel();
            model.SetSelectedPath(path, overwriteConfirmed: true);
            Assert.False(model.NeedsOverwriteConfirmation);
            model.ApplyTranslationTag(tag);
            Assert.Equal(path, model.FileName);
            Assert.True(model.NeedsOverwriteConfirmation);
        }
        finally { Directory.Delete(folder, true); }
    }
}
