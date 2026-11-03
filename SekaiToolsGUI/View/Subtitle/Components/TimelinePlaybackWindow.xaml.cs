using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.Service;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class TimelinePlaybackWindow : Window
{
    public TimelinePlaybackWindow()
    {
        InitializeComponent();
        WindowWorkArea.Attach(this);
        Owner = Application.Current.MainWindow;
    }

    public Image PlaybackImageElement => PlaybackImage;
}