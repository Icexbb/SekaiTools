using System.Windows.Controls;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Subtitle;

namespace SekaiToolsGUI.View.Subtitle;

public partial class SubtitlePage : UserControl, IAppPage<SubtitlePageModel>
{
    public SubtitlePage()
    {
        InitializeComponent();
        DataContext = TaskComponent.ViewModel;
    }

    public SubtitlePageModel ViewModel => TaskComponent.ViewModel;

    public void OnNavigatedTo() => TaskComponent.OnNavigatedTo();
}
