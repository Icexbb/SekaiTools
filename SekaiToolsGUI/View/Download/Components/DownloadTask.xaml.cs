using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiToolsCore;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadTask : UserControl
{
    public DownloadTask(string scriptTag, string url)
    {
        InitializeComponent();
        Url = url;
        ScriptTag = scriptTag;
        var filename = Path.GetFileName(url);
        SavePath = Path.Combine(ResourceManager.DataBaseDir, "Scripts", filename);
        DataContext = this;
        TaskNameTextBlock.Text = string.Join("\n", ScriptTag.Split("|"));
    }

    public string ScriptTag { get; set; }

    public string Url { get; set; }

    public string SavePath { get; set; }

    public bool Downloaded { get; private set; }

    public event EventHandler? RemoveRequested;

    public void SetCanRemove(bool canRemove)
    {
        RemoveButton.IsEnabled = canRemove;
    }

    private void RemoveButton_OnClick(object sender, RoutedEventArgs e)
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ChangeStatus(int status)
    {
        if (status == 1) Downloaded = true;

        Dispatcher.Invoke(() =>
        {
            (StatusIcon.Symbol, StatusText.Text, StatusText.Foreground) = status switch
            {
                0 => (SymbolRegular.ArrowSync24, "下载中", new SolidColorBrush(Colors.DodgerBlue)),
                1 => (SymbolRegular.DocumentCheckmark24, "下载完成", new SolidColorBrush(Colors.MediumSeaGreen)),
                2 => (SymbolRegular.DocumentDismiss24, "下载失败", new SolidColorBrush(Colors.IndianRed)),
                _ => (SymbolRegular.ArrowDownload24, "等待下载", Foreground)
            };
            Control.BorderBrush = status switch
            {
                0 => new SolidColorBrush(Colors.LightBlue),
                1 => new SolidColorBrush(Colors.LightGreen),
                2 => new SolidColorBrush(Colors.LightPink),
                _ => null
            };
            Control.BorderThickness = new Thickness(2);
        });
    }
}