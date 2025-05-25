using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Setting.Components;

public partial class DownloadSourceEditor : UserControl
{
    public DownloadSourceEditorModel ViewModel
    {
        get => (DownloadSourceEditorModel)DataContext;
        set => DataContext = value;
    }


    public DownloadSourceEditor()
    {
        InitializeComponent();
    }


    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Delete();
    }
}