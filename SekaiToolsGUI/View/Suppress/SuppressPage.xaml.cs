using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SekaiToolsGUI.ViewModel;
using Wpf.Ui.Abstractions.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace SekaiToolsGUI.View.Suppress;

public partial class SuppressPage : UserControl, INavigableView<SuppressPageModel>
{
    public SuppressPage()
    {
        DataContext = SuppressPageModel.Instance;
        InitializeComponent();
    }

    public SuppressPageModel ViewModel => (SuppressPageModel)DataContext;

    private static string? SelectFile(object sender, RoutedEventArgs e, string filter)
    {
        var openFileDialog = new OpenFileDialog { Filter = filter };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private void VideoFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "视频文件|*.mp4;*.avi;*.mkv;*.webm;*.wmv");
        if (result == null) return;

        ViewModel.SourceVideo = result;
    }

    private void ScriptFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectFile(sender, e, "字幕文件|*.ass");
        if (result == null) return;

        ViewModel.SourceSubtitle = result;
    }

    private void SaveFileBrowser_OnClick(object sender, RoutedEventArgs e)
    {
        var result = SelectSavePath();
        if (result == null) return;

        ViewModel.OutputPath = result;
    }

    private string? SelectSavePath()
    {
        var openFileDialog = new SaveFileDialog
        {
            Filter = "Mp4 文件|*.mp4;",
            DefaultDirectory = Path.GetDirectoryName(ViewModel.SourceVideo),
            DefaultExt = ".mp4",
            FileName = Path.ChangeExtension(Path.GetFileName(ViewModel.SourceVideo), ".txt")
        };
        var result = openFileDialog.ShowDialog();
        return result == true ? openFileDialog.FileName : null;
    }

    private async void StartSuppress_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Suppressor.Instance.Suppress();
        }
        catch (Exception exc)
        {
            await Dispatcher.Invoke<Task>(async () =>
            {
                var uiMessageBox = new MessageBox
                {
                    Title = "视频处理出错",
                    Content = exc.Message + "\n" + exc.StackTrace
                };

                await uiMessageBox.ShowDialogAsync();
                ViewModel.Running = false;
            });
            if (Debugger.IsAttached) throw;
        }
    }


    private void DisposeButton_OnClick(object sender, RoutedEventArgs e)
    {
        Suppressor.Instance.Clean();
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        Suppressor.Instance.Clean();
        ViewModel.Reset();
    }

    private void ShowFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        ShowFile(ViewModel.OutputPath);
        return;

        void ShowFile(string path)
        {
            var psi = new ProcessStartInfo("Explorer.exe")
            {
                Arguments = "/e,/select," + path
            };
            Process.Start(psi);
        }
    }

    private void StatusTextChange_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender!;
        box.ScrollToEnd();
    }
}