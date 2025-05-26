using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Emgu.CV;
using Microsoft.Win32;
using SekaiToolsCore;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsCore.Process.Model;
using SekaiToolsGUI.View.Subtitle.Components;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using SaveFileDialog = SekaiToolsGUI.View.Subtitle.Components.SaveFileDialog;

namespace SekaiToolsGUI.View.Subtitle;

public partial class SubtitlePage : UserControl, INavigableView<SubtitlePageModel>
{
    public SubtitlePage()
    {
        DataContext = new SubtitlePageModel();
        InitializeComponent();
    }


    private static ISnackbarService SnackService =>
        (Application.Current.MainWindow as MainWindow)?.WindowSnackbarService!;

    public SubtitlePageModel ViewModel => (SubtitlePageModel)DataContext;


    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private async Task SelectSameNameFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();

        string[] videoExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        string[] jsonExt = [".json", ".asset"];
        string[] txtExt = [".txt"];

        if (videoExt.Contains(fileExt))
        {
            ViewModel.VideoFilePath = filename;

            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (scriptPath == null && translatePath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (jsonExt.Contains(fileExt))
        {
            ViewModel.ScriptFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var translatePath = txtExt.Select(te => Path.ChangeExtension(filename, te)).FirstOrDefault(File.Exists);

            if (videoPath == null && translatePath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (translatePath != null) ViewModel.TranslateFilePath = translatePath;
        }
        else if (txtExt.Contains(fileExt))
        {
            ViewModel.TranslateFilePath = filename;

            var videoPath = videoExt.Select(ve => Path.ChangeExtension(filename, ve)).FirstOrDefault(File.Exists);
            var scriptPath = jsonExt.Select(se => Path.ChangeExtension(filename, se)).FirstOrDefault(File.Exists);

            if (videoPath == null && scriptPath == null) return;

            var dialogResult = await ShowDialog();
            if (!dialogResult) return;
            if (videoPath != null) ViewModel.VideoFilePath = videoPath;
            if (scriptPath != null) ViewModel.ScriptFilePath = scriptPath;
        }

        return;

        async Task<bool> ShowDialog()
        {
            var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
            var token = new CancellationToken();
            var dialogResult = await dialogService.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "在该文件处发现了同名的文件，是否自动引入作为处理文件？",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否"
                }, token);
            return dialogResult == ContentDialogResult.Primary;
        }
    }


    private async void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        await SelectSameNameFile(result);
    }

    private async void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情脚本文件|*.json;*.asset");
        if (result == null) return;

        await SelectSameNameFile(result);
    }

    private async void TranslationFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "剧情翻译文件|*.txt");
        if (result == null) return;

        await SelectSameNameFile(result);
    }


    private void ControlButton_OnClick(object sender, EventArgs arg)
    {
        if (CheckConfig()) StartProcess();
        return;

        bool CheckConfig()
        {
            var vfp = ViewModel.VideoFilePath;
            var sfp = ViewModel.ScriptFilePath;
            var tfp = ViewModel.TranslateFilePath;
            if (string.IsNullOrEmpty(vfp) || string.IsNullOrEmpty(sfp) || string.IsNullOrEmpty(tfp))
            {
                SnackService.Show("错误", "请填写完整的文件路径", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.TextGrammarDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(vfp))
            {
                SnackService.Show("错误", "视频文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(sfp))
            {
                SnackService.Show("错误", "剧情脚本文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            if (!File.Exists(tfp))
            {
                SnackService.Show("错误", "剧情翻译文件不存在", ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                return false;
            }

            return true;
        }
    }


    private void LinePanel_AddDialogLine(DialogFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;
            var line = new DialogLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }


    private void LinePanel_AddBannerLine(BannerFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new BannerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private void LinePanel_AddMarkerLine(MarkerFrameSet set)
    {
        Dispatcher.Invoke(() =>
        {
            var needScroll = Math.Abs(LineViewer.ScrollableHeight - LineViewer.VerticalOffset) < 1;

            var line = new MarkerLine(set)
            {
                Margin = new Thickness(5, 5, 10, 5)
            };
            LinePanel.Children.Add(line);
            if (needScroll) LineViewer.ScrollToEnd();
        });
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopProcess();
        ViewModel.Reset();
        LinePanel.Children.Clear();
        TextBlockProgression.Text = "";
        TextBlockFps.Text = "";
        ProgressBarProgression.Value = 0;
    }


    private async void OutputButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;

        var dialog = new SaveFileDialog(dialogService.GetDialogHost() ?? throw new InvalidOperationException(),
            ViewModel.VideoFilePath);
        var token = new CancellationToken();
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;
        var fileName = dialog.ViewModel.FileName;

        var subtitle = GenerateSubtitle();
        await File.WriteAllTextAsync(fileName, subtitle.ToString(), Encoding.UTF8, token);

        SnackService.Show("成功", "字幕文件已保存", ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
        ExplorerHelper.OpenFolderAndFocus(fileName);
    }


    private async void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        var data = e.Data.GetData(DataFormats.FileDrop)!;
        var fileName = ((Array)data).GetValue(0)!.ToString();
        if (!File.Exists(fileName)) return;

        await GetSameBaseFile(fileName);
    }

    private async Task GetSameBaseFile(string filename)
    {
        var fileExt = Path.GetExtension(filename).ToLower();
        List<string> vExt = [".mp4", ".avi", ".mkv", ".webm", ".wmv"];
        List<string> sExt = [".json", ".asset"];
        List<string> tExt = [".txt"];
        if (vExt.Contains(fileExt) || sExt.Contains(fileExt) || tExt.Contains(fileExt))
            await SelectSameNameFile(filename);
        else
            SnackService.Show("错误", "文件格式不支持", ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.DocumentError24),
                new TimeSpan(0, 0, 3));
    }

    private void UIElement_OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Link
            : DragDropEffects.None;
    }

    private void OnlyTooLongSwitch_OnClick(object sender, RoutedEventArgs e)
    {
        var targetVisibility = OnlyTooLongSwitch.IsChecked ?? false ? Visibility.Collapsed : Visibility.Visible;
        foreach (var child in LinePanel.Children)
            switch (child)
            {
                case DialogLine dialogLine:
                    if (dialogLine.ViewModel.Set.NeedSetSeparator) continue;
                    dialogLine.Visibility = targetVisibility;
                    break;
                case BannerLine bannerLine:
                    bannerLine.Visibility = targetVisibility;
                    break;
                case MarkerLine markerLine:
                    markerLine.Visibility = targetVisibility;
                    break;
            }
    }
}

public partial class SubtitlePage
{
    private CancellationTokenSource? TokenSource { get; } = new();
    private CancellationToken CancellationToken => TokenSource!.Token;

    private VideoProcessor? VideoProcessor { get; set; } = null;

    private void StopProcess()
    {
        VideoProcessor?.StopProcess();
    }

    private SekaiToolsCore.SubStationAlpha.Subtitle GenerateSubtitle()
    {
        List<BannerFrameSet> bannerFrameSets = [];
        List<DialogFrameSet> dialogFrameSets = [];
        List<MarkerFrameSet> markerFrameSets = [];
        foreach (var child in LinePanel.Children)
            switch (child)
            {
                case DialogLine dialogLine:
                    var set = dialogLine.ViewModel.Set;
                    set.Data.BodyTranslated = set.Data.BodyTranslated.Replace("…", "..."); // 修正省略号
                    dialogFrameSets.Add(dialogLine.ViewModel.Set);
                    break;
                case BannerLine bannerLine:
                    bannerFrameSets.Add(bannerLine.ViewModel.Set);
                    break;
                case MarkerLine markerLine:
                    markerFrameSets.Add(markerLine.ViewModel.Set);
                    break;
            }

        if (VideoProcessor == null) throw new NullReferenceException();
        return VideoProcessor.GenerateSubtitle(bannerFrameSets, dialogFrameSets, markerFrameSets);
    }

    private void StartProcess()
    {
        var settings = SettingPageModel.Instance;

        VideoProcessor = new VideoProcessor(new Config(
                ViewModel.VideoFilePath,
                ViewModel.ScriptFilePath,
                ViewModel.TranslateFilePath,
                settings.GetStyleFontConfig(),
                settings.GetExportStyleConfig(),
                settings.GetTypewriterSetting(),
                settings.GetMatchingThreshold()
            ), new VideoProcessCallbacks
            {
                OnTaskFinished = () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ViewModel.IsRunning = false;
                        if (!VideoProcessor?.Finished ?? false)
                        {
                            SnackService.Show("错误", "运行结束", ControlAppearance.Danger,
                                new SymbolIcon(SymbolRegular.DocumentDismiss24), new TimeSpan(0, 0, 3));
                        }
                        else
                        {
                            ViewModel.IsFinished = true;
                            SnackService.Show("成功", "运行结束", ControlAppearance.Success,
                                new SymbolIcon(SymbolRegular.DocumentCheckmark24), new TimeSpan(0, 0, 3));
                        }

                        TextBlockEta.Text = "";
                    });
                },
                OnTaskStarted = () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ViewModel.IsRunning = true;
                        ViewModel.HasNotStarted = false;
                    });
                },
                OnProgress = progression =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBarProgression.Value = progression;
                        ProgressBarProgression.Maximum = 1;
                        TextBlockProgression.Text = $"{progression:P}";
                    });
                },
                OnFramePreviewImage = frame =>
                {
                    Dispatcher.Invoke(() => { ViewModel.FramePreviewImage = frame.ToBitmapSource(); });
                },
                OnNewDialog = LinePanel_AddDialogLine,
                OnNewBanner = LinePanel_AddBannerLine,
                OnNewMarker = LinePanel_AddMarkerLine,
                OnException = e =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        var uiMessageBox = new MessageBox
                        {
                            Title = "视频处理出错",
                            Content = e.Message + "\n" + e.StackTrace
                        };

                        await uiMessageBox.ShowDialogAsync(cancellationToken: CancellationToken);
                        ViewModel.IsRunning = false;
                    });
                },
                OnFps = (fps, eta) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TextBlockFps.Text = $"FPS: {fps}";
                        TextBlockEta.Text = eta.TotalMilliseconds > 1000 ? $"ETA: {eta.Remains()}" : "";
                    });
                },
            }
        );
        VideoProcessor.StartProcess();
    }
}