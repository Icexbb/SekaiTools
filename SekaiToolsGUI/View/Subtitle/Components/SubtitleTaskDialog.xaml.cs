using System.IO;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class SubtitleTaskDialog : ContentDialog
{
    public SubtitleTaskDialog(ContentDialogHost host) : base(host)
    {
        ViewModel = new SubtitlePageModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public SubtitlePageModel ViewModel { get; }

    protected override void OnButtonClick(ContentDialogButton button)
    {
        if (button == ContentDialogButton.Primary)
        {
            if (!ViewModel.CanStart || !File.Exists(ViewModel.VideoFilePath) ||
                !File.Exists(ViewModel.ScriptFilePath) || !File.Exists(ViewModel.TranslateFilePath))
            {
                ValidationError.Text = "请选择存在的视频、剧本和翻译文件";
                return;
            }
        }
        base.OnButtonClick(button);
    }
}
