using Avalonia.Controls;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsAvalonia.ViewModel.Subtitle;

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
