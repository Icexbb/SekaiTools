using Avalonia.Controls;

namespace SekaiToolsAvalonia.View.Download.Components;

public partial class DownloadTask : UserControl
{
    public DownloadTask(string tag, string url)
    {
        InitializeComponent();
        Tag = tag;
        Url = url;
        TagText.Text = tag;
    }

    public new string Tag { get; }
    public string Url { get; }
    public bool Downloaded { get; set; }

    public void ChangeStatus(int status)
    {
        TagText.Text = status switch
        {
            0 => $"[下载中] {Tag}",
            1 => $"[完成] {Tag}",
            2 => $"[失败] {Tag}",
            _ => Tag
        };
        if (status == 1) Downloaded = true;
    }
}
