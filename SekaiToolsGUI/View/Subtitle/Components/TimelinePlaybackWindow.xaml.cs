using System.Windows;
using System.Windows.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class TimelinePlaybackWindow : Window
{
    public TimelinePlaybackWindow()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    public Image PlaybackImageElement => PlaybackImage;
}
