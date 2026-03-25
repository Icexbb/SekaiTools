using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.Translation;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui;
using Wpf.Ui.Controls;
using SaveFileDialog = SekaiToolsGUI.View.Translate.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage : UserControl
{
    private string _scriptPath = "";

    private string _translationPath = "";

    public TranslatePage()
    {
        InitializeComponent();
        DataContext = new TranslatePageModel();
        // TestLoad();
    }

    public TranslatePageModel ViewModel => (TranslatePageModel)DataContext;


    private static ISnackbarService SnackbarService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

    private void TestLoad()
    {
        _scriptPath = @"E:\ProjectSekai\test\aprilfool_2024_01.json";
        ViewModel.Story = Story.FromFile(_scriptPath);
    }

    private void LoadFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "剧本文件|*.json;*.asset"
        };

        if (openFileDialog.ShowDialog() != true) return;

        Story story;
        try
        {
            story = Story.FromFile(openFileDialog.FileName);
            _scriptPath = openFileDialog.FileName;
        }
        catch (Exception exception)
        {
            SnackbarService.Show("错误", exception.Message, ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
            return;
        }

        ViewModel.Story = story;
        SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
    }

    private void LoadTranslationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty)
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

        var tData = new TranslationData(filePath);
        foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

        var gData = new GameScript(_scriptPath);

        if (tData.IsApplicable(gData))
        {
            _translationPath = filePath;
            ViewModel.Story = new Story(gData, tData);
            SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
        }
        else
        {
            SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
        }
    }

    private void LoadReviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsEmpty)
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

        var tData = new TranslationData(filePath);
        foreach (var t in tData.Translations) t.Body = t.Body.Replace("\\N", "\n");

        var gData = new GameScript(_scriptPath);

        if (tData.IsApplicable(gData))
        {
            _translationPath = filePath;
            ViewModel.ApplyReference(new Story(gData, tData));
            SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
        }
        else
        {
            SnackbarService.Show("错误", "翻译数据不适用于此剧本", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentDismiss24), TimeSpan.FromSeconds(3));
        }
    }


    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Clear();
        _scriptPath = "";
        _translationPath = "";
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
            dialogService.GetDialogHost() ?? throw new InvalidOperationException(),
            _scriptPath, _translationPath);
        var token = new CancellationToken();
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
}