using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SekaiToolsGUI.View.Download.Components;

public partial class DownloadTask : UserControl
{
    public DownloadTask(string scriptTag, string url)
    {
        InitializeComponent();
        Url = url;
        ScriptTag = scriptTag;
        var filename = Path.GetFileName(url);
        SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SekaiTools",
            "Scripts", filename);
        DataContext = this;
    }

    public string ScriptTag { get; set; }

    public string Url { get; set; }

    public string SavePath { get; set; }

    public bool Downloaded { get; private set; }

    public void ChangeStatus(int status)
    {
        if (status == 1) Downloaded = true;

        Dispatcher.Invoke(() =>
        {
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