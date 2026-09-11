using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SekaiToolsBase;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsGUI.View.Download;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using SaveFileDialog = SekaiToolsGUI.View.Translate.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage : UserControl
{
    public TranslatePage()
    {
        InitializeComponent();
        DataContext = new TranslatePageModel();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TranslatePageModel.ScriptPath)
                or nameof(TranslatePageModel.TranslationPath)
                or nameof(TranslatePageModel.ReferenceTranslationPath))
                UpdateLoadButtons();
        };
        UpdateLoadButtons();
    }

    public TranslatePageModel ViewModel => (TranslatePageModel)DataContext;


    private static ISnackbarService SnackbarService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    private async void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasScript)
        {
            if (!await ConfirmReplaceCurrentContentAsync()) return;
            ViewModel.Clear();
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "剧本文件|*.json;*.asset"
        };

        if (openFileDialog.ShowDialog() != true) return;

        GameScript script;
        try
        {
            script = new GameScript(openFileDialog.FileName);
            // 先验证能够转换，再更新当前页面状态。
            _ = new Story(script, new TranslationData(null));
        }
        catch (Exception exception)
        {
            SnackbarService.Show("错误", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        if (!ViewModel.IsEmpty && !await ConfirmReplaceCurrentContentAsync()) return;

        ViewModel.LoadScript(script, openFileDialog.FileName);
        SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
    }

    private async void LoadTranslationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasTranslation)
        {
            if (!await ConfirmReplaceCurrentContentAsync()) return;
            ViewModel.ClearTranslation();
            return;
        }

        if (!ViewModel.HasScript)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt"
        };

        if (openFileDialog.ShowDialog() != true) return;
        var filePath = openFileDialog.FileName;

        try
        {
            var tData = new TranslationData(filePath);
            foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

            if (ViewModel.IsTranslationApplicable(tData))
            {
                if (!await ConfirmReplaceCurrentContentAsync()) return;

                ViewModel.LoadTranslation(tData, filePath);
                Logger.Log($"翻译载入成功: 剧本={ViewModel.ScriptPath}, 翻译={filePath}, 对话={tData.Translations.Count}");
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                Logger.Log($"翻译不适用于剧本: 翻译={filePath}", LogLevel.Warning);
                SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"翻译载入失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("错误", $"载入失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        }
    }

    private void LoadReviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasReferenceTranslation)
        {
            ViewModel.ClearReference();
            SnackbarService.Show("已清除", "参考翻译已清除", ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Info24), TimeSpan.FromSeconds(2));
            return;
        }

        if (!ViewModel.HasScript)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt"
        };

        if (openFileDialog.ShowDialog() != true) return;
        var filePath = openFileDialog.FileName;

        try
        {
            var tData = new TranslationData(filePath);
            foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

            if (ViewModel.IsTranslationApplicable(tData))
            {
                ViewModel.LoadReferenceTranslation(tData, filePath);
                SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
            }
            else
            {
                SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"参考翻译载入失败: {ex.Message}", LogLevel.Error);
            SnackbarService.Show("错误", $"载入失败: {ex.Message}", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(5));
        }
    }


    private async void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasScript || !await ConfirmReplaceCurrentContentAsync()) return;

        ViewModel.Clear();
    }

    private void OpenDownloadPageButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
            mainWindow.Navigate(typeof(DownloadPage));
    }

    private void UpdateLoadButtons()
    {
        UpdateButton(LoadScriptButton, ViewModel.ScriptPath, "载入剧本", "清除剧本");
        UpdateButton(LoadTranslationButton, ViewModel.TranslationPath, "载入翻译文件", "清除翻译");
        UpdateButton(ReferenceTranslationButton, ViewModel.ReferenceTranslationPath, "载入参考翻译文件", "清除参考翻译");
        UpdateFilePath(TextBlockScriptFile, ViewModel.ScriptPath);
        UpdateFilePath(TextBlockTranslation, ViewModel.TranslationPath);
        UpdateFilePath(TextBlockTranslationReference, ViewModel.ReferenceTranslationPath);

        static void UpdateButton(Wpf.Ui.Controls.Button button, string path, string loadText, string clearText)
        {
            var loaded = !string.IsNullOrEmpty(path);
            button.Content = loaded ? clearText : loadText;
            button.Appearance = loaded ? ControlAppearance.Caution : ControlAppearance.Secondary;
            button.ToolTip = loaded ? path : null;
        }
    }

    private static void UpdateFilePath(System.Windows.Controls.TextBlock textBlock, string path)
    {
        textBlock.ToolTip = string.IsNullOrEmpty(path) ? null : path;
        textBlock.Text = CompactPath(path);

        string CompactPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || Fits(fullPath)) return fullPath;

            var root = Path.GetPathRoot(fullPath) ?? string.Empty;
            // 从前向后省略目录，优先保留完整文件名及末尾目录。
            for (var i = root.Length; i < fullPath.Length; i++)
            {
                if (fullPath[i] is not ('\\' or '/')) continue;
                var candidate = root + ".." + fullPath[i..];
                if (Fits(candidate)) return candidate;
            }

            // 文件名本身过长时仍从中间省略，保留路径开头和扩展名。
            for (var keep = fullPath.Length - 1; keep > 0; keep--)
            {
                var prefixLength = Math.Min(root.Length, keep / 2);
                var candidate = fullPath[..prefixLength] + ".." + fullPath[^(keep - prefixLength)..];
                if (Fits(candidate)) return candidate;
            }

            return "..";
        }

        bool Fits(string text)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, textBlock.FlowDirection,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(textBlock).PixelsPerDip);
            return formatted.WidthIncludingTrailingWhitespace <= textBlock.MaxWidth;
        }
    }

    private static async Task<bool> ConfirmReplaceCurrentContentAsync()
    {
        var dialogService = ((MainWindow)Application.Current.MainWindow!).WindowContentDialogService;
        var result = await dialogService.ShowSimpleDialogAsync(new SimpleContentDialogCreateOptions
        {
            Title = "替换当前内容？",
            Content = "当前翻译内容将被清除，未保存的修改无法恢复。",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消"
        }, CancellationToken.None);
        return result == ContentDialogResult.Primary;
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty)
        {
            SnackbarService.Show("错误", "请先载入剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new SaveFileDialog(
            dialogService.GetDialogHostEx() ?? throw new InvalidOperationException(),
            ViewModel.ScriptPath, ViewModel.TranslationPath);
        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;
        var fileName = dialog.ViewModel.FileName;


        var content = ExportTranslation();
        await File.WriteAllTextAsync(fileName, content, token);

        var snackService = (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;
        snackService.Show("成功", "翻译文件文件已保存", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
        ShowFile(fileName);

        return;

        void ShowFile(string path)
        {
            var psi = new ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            Process.Start(psi);
        }

        string ExportTranslation()
        {
            return ViewModel.Result;
        }
    }

    private void SpecialCharButton_OnClick(object sender, RoutedEventArgs e)
    {
        SpecialCharPopover.Open();
    }

    private void SpecialCharacters_OnCustomCharacterAdding(object? sender, EventArgs e)
    {
        SpecialCharPopover.Close();
    }
}
