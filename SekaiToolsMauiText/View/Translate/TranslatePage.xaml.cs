using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsMauiText.ViewModel;

namespace SekaiToolsMauiText.View.Translate;

public partial class TranslatePage : ContentPage
{
    private string _scriptPath = "";
    private string _translationPath = "";

    public TranslatePage()
    {
        InitializeComponent();
        BindingContext = new TranslatePageModel();
    }

    private TranslatePageModel ViewModel => (TranslatePageModel)BindingContext;

    private void UpdateToolbarVisibility()
    {
        var hasEvents = !ViewModel.IsEmpty;
        LoadTranslationButton.IsVisible = hasEvents;
        LoadReviewButton.IsVisible = hasEvents;
    }

    private async void LoadScriptButton_OnClick(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择剧本文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".json", ".asset"] },
                    { DevicePlatform.Android, ["application/json", "*/*"] },
                    { DevicePlatform.iOS, ["public.json", "public.data"] },
                    { DevicePlatform.MacCatalyst, ["public.json", "public.data"] }
                })
            });

            if (result == null) return;

            Story story;
            try
            {
                story = Story.FromFile(result.FullPath);
                _scriptPath = result.FullPath;
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("错误", ex.Message, "确定");
                return;
            }

            ViewModel.Story = story;
            UpdateToolbarVisibility();
            await DisplayAlertAsync("成功", "成功载入剧本", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", ex.Message, "确定");
        }
    }

    private async void LoadTranslationButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty)
        {
            await DisplayAlertAsync("错误", "请先载入剧本", "确定");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择翻译文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".txt"] },
                    { DevicePlatform.Android, ["text/plain"] },
                    { DevicePlatform.iOS, ["public.plain-text"] },
                    { DevicePlatform.MacCatalyst, ["public.plain-text"] }
                })
            });

            if (result == null) return;

            var tData = new TranslationData(result.FullPath);
            foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

            var gData = new GameScript(_scriptPath);
            if (!tData.IsApplicable(gData))
            {
                await DisplayAlertAsync("错误", "翻译数据不适用于此剧本", "确定");
                return;
            }

            _translationPath = result.FullPath;
            ViewModel.Story = new Story(gData, tData);
            UpdateToolbarVisibility();
            await DisplayAlertAsync("成功", "成功载入翻译", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", ex.Message, "确定");
        }
    }

    private async void LoadReviewButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty)
        {
            await DisplayAlertAsync("错误", "请先载入剧本", "确定");
            return;
        }

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "选择对照翻译文件",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".txt"] },
                    { DevicePlatform.Android, ["text/plain"] },
                    { DevicePlatform.iOS, ["public.plain-text"] },
                    { DevicePlatform.MacCatalyst, ["public.plain-text"] }
                })
            });

            if (result == null) return;

            var tData = new TranslationData(result.FullPath);
            foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

            var gData = new GameScript(_scriptPath);
            if (!tData.IsApplicable(gData))
            {
                await DisplayAlertAsync("错误", "翻译数据不适用于此剧本", "确定");
                return;
            }

            ViewModel.ApplyReference(new Story(gData, tData));
            await DisplayAlertAsync("成功", "成功载入对照翻译", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", ex.Message, "确定");
        }
    }

    private void ResetButton_OnClick(object sender, EventArgs e)
    {
        ViewModel.Clear();
        _scriptPath = "";
        _translationPath = "";
        UpdateToolbarVisibility();
    }

    private async void SaveButton_OnClick(object sender, EventArgs e)
    {
        if (ViewModel.IsEmpty)
        {
            await DisplayAlertAsync("错误", "请先载入剧本", "确定");
            return;
        }

        var defaultPath = _translationPath != ""
            ? _translationPath
            : Path.ChangeExtension(_scriptPath, ".txt");

        var filePath = await DisplayPromptAsync(
            "保存翻译文件",
            "文件将保存到（可修改路径）：",
            "保存", "取消",
            defaultPath);

        if (filePath == null) return;

        try
        {
            var content = ViewModel.Result;
            await File.WriteAllTextAsync(filePath, content);
            await DisplayAlertAsync("成功", $"翻译文件已保存到：\n{filePath}", "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("错误", $"保存失败：{ex.Message}", "确定");
        }
    }
}