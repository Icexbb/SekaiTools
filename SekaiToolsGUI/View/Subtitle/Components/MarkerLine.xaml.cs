using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public class MarkerLineModel(MarkerFrameSet set) : ViewModelBase
{
    private readonly FrameRate _frameRate = set.Fps;
    public readonly MarkerFrameSet Set = set;

    public int Index => Set.Data.Index;
    public string Content => Set.Data.BodyOriginal;

    public int StartFrame => Set.Start().Index;
    public string StartTime => Set.StartTime();
    public int EndFrame => Set.End().Index;
    public string EndTime => Set.EndTime();
}

public partial class MarkerLine : UserControl, INavigableView<MarkerLineModel>
{
    public MarkerLine(MarkerFrameSet set)
    {
        DataContext = new MarkerLineModel(set);
        InitializeComponent();
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }

    public MarkerLineModel ViewModel => (MarkerLineModel)DataContext;

    private void TextBlockContent_OnMouseEnter(object sender, MouseEventArgs e)
    {
        TextBlockContent.Text = ViewModel.Set.Data.BodyOriginal;
    }

    private void TextBlockContent_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel.Set.Data.BodyTranslated.Length > 0)
            TextBlockContent.Text = ViewModel.Set.Data.BodyTranslated;
    }
}