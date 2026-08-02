using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class MarkerLine : UserControl, INavigableView<MarkerLineModel>
{
    public MarkerLine(MarkerBaseFrameSet set)
    {
        DataContext = new MarkerLineModel(set);
        InitializeComponent();
    }

    public MarkerLineModel ViewModel => (MarkerLineModel)DataContext;

    private void MarkerLine_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        StartQuickEditDialog();
    }

    private void QuickEditBtn_OnClick(object sender, RoutedEventArgs e)
    {
        StartQuickEditDialog();
    }

    private async void StartQuickEditDialog()
    {
        var dialogService = (Application.Current.MainWindow as MainWindow)?.WindowContentDialogService!;
        var dialog = new QuickEditDialog(ViewModel.Set);
        var dialogResult = await dialogService.ShowAsync(dialog, CancellationToken.None);
        if (dialogResult != ContentDialogResult.Primary) return;

        ViewModel.TranslatedContent = dialog.ViewModel.ContentTranslated;
    }
}