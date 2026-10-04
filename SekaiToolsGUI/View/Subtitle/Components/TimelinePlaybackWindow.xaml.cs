using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class TimelinePlaybackWindow : Window
{
    public TimelinePlaybackWindow()
    {
        InitializeComponent();
        SekaiToolsGUI.Service.WindowWorkArea.Attach(this);
        Owner = Application.Current.MainWindow;
    }

    public Image PlaybackImageElement => PlaybackImage;
}
