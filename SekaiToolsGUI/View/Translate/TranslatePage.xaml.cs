using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SekaiToolsBase.GameScript;
using SekaiToolsBase.Story;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsBase.Story.Translation;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.View.Translate.Components;
using Wpf.Ui;
using Wpf.Ui.Controls;
using SaveFileDialog = SekaiToolsGUI.View.Translate.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Translate;

public partial class TranslatePage : UserControl
{
    private string _scriptPath = "";

    private Story? _story;
    private string _translationPath = "";

    public TranslatePage()
    {
        InitializeComponent();
    }

    private Story? Story
    {
        get => _story;
        set
        {
            _story = value;
            Dispatcher.Invoke(TranslatePanel.Children.Clear);
            if (_story is null) return;
            foreach (var @event in _story.Events)
                AddLine(@event);
            return;

            void AddLine(BaseStoryEvent baseStoryEvent)
            {
                Dispatcher.Invoke(() =>
                {
                    if (baseStoryEvent is DialogStoryEvent dialog)
                    {
                        TranslatePanel.Children.Add(new TranslateLineDialog(dialog)
                        {
                            Margin = new Thickness(5)
                        });
                        if (dialog.CloseWindow)
                            TranslatePanel.Children.Add(new TranslateLineEmpty
                            {
                                Margin = new Thickness(5)
                            });
                    }
                    else
                    {
                        TranslatePanel.Children.Add(new TranslateLineEffect(baseStoryEvent)
                        {
                            Margin = new Thickness(5)
                        });
                    }
                });
            }
        }
    }

    private static ISnackbarService SnackbarService =>
        ((MainWindow)Application.Current.MainWindow!).WindowSnackbarService;

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

        Story = story;
        SnackbarService.Show("成功", "成功载入", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), TimeSpan.FromSeconds(3));
    }

    private void LoadTranslationButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Story is null)
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
            Story = new Story(gData, tData);
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
        Story = null;
        _scriptPath = "";
        _translationPath = "";
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Story is null)
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
            var translation = new StringBuilder();
            foreach (UIElement child in TranslatePanel.Children)
                if (child is IExportable exportable)
                    translation.AppendLine(exportable.Export());

            return translation.ToString();
        }
    }
}