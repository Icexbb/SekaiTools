using Avalonia.Controls;
using SekaiToolsAvalonia.ViewModel.Subtitle;
using SekaiToolsCore.Process.FrameSet;

namespace SekaiToolsAvalonia.View.Subtitle.Components;

public partial class MarkerLine : UserControl
{
    public MarkerLine(MarkerBaseFrameSet set)
    {
        DataContext = new MarkerLineModel(set);
        InitializeComponent();
    }

    public MarkerLineModel ViewModel => (MarkerLineModel)DataContext!;
}