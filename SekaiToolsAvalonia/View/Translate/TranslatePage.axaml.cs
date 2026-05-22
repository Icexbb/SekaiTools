using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.ViewModel.Translate;

namespace SekaiToolsAvalonia.View.Translate;

public partial class TranslatePage : UserControl, IAppPage
{
    private string _scriptPath = "";
    private string _translationPath = "";

    public TranslatePage()
    {
        DataContext = new TranslatePageModel();
        InitializeComponent();
    }

    public void OnNavigatedTo() { }

    private TranslatePageModel ViewModel => (TranslatePageModel)DataContext!;

    private async void LoadFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择剧本文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("剧本文件") { Patterns = ["*.json", "*.asset"] }]
        });

        if (files.Count == 0) return;
        var filePath = files[0].Path.LocalPath;

        try
        {
            var story = Story.FromFile(filePath);
            _scriptPath = filePath;
            ViewModel.Story = story;
            Logger.Log($"剧本载入成功: {filePath}", LogLevel.Information);
        }
        catch (Exception ex)
        {
            Logger.Log($"剧本载入失败: {ex.Message}", LogLevel.Error);
        }
    }

    private async void LoadTranslationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择翻译文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("文本文件") { Patterns = ["*.txt"] }]
        });

        if (files.Count == 0) return;
        var filePath = files[0].Path.LocalPath;

        try
        {
            var tData = new TranslationData(filePath);
            foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");
            var gData = new GameScript(_scriptPath);

            if (tData.IsApplicable(gData))
            {
                _translationPath = filePath;
                ViewModel.Story = new Story(gData, tData);
                Logger.Log($"翻译载入成功: {filePath}", LogLevel.Information);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译载入失败: {ex.Message}", LogLevel.Error);
        }
    }

    private async void LoadReviewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择对照翻译文件",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("文本文件") { Patterns = ["*.txt"] }]
        });

        if (files.Count == 0) return;
        var filePath = files[0].Path.LocalPath;

        var tData = new TranslationData(filePath);
        foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");
        var gData = new GameScript(_scriptPath);

        if (tData.IsApplicable(gData))
        {
            _translationPath = filePath;
            ViewModel.ApplyReference(new Story(gData, tData));
        }
    }

    private void ResetButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.Clear();
        _scriptPath = "";
        _translationPath = "";
    }

    private async void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var defaultName = Path.ChangeExtension(Path.GetFileName(_scriptPath), ".txt");
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存翻译文件",
            SuggestedFileName = _translationPath != "" ? Path.GetFileName(_translationPath) : defaultName,
            FileTypeChoices = [new FilePickerFileType("文本文件") { Patterns = ["*.txt"] }]
        });

        if (file == null) return;

        var content = ViewModel.Result;
        await File.WriteAllTextAsync(file.Path.LocalPath, content);
    }
}
