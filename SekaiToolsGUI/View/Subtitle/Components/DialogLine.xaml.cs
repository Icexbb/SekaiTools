using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class DialogLine : UserControl, INavigableView<DialogLineModel>
{
    public DialogLine(DialogBaseFrameSet set)
    {
        set.InitSeparator();
        DataContext = new DialogLineModel(set, CharTime);
        InitializeComponent();
        CheckLineExpander();
    }

    private int CharTime => SettingPageModel.Instance.TypewriterCharTime;

    public DialogLineModel ViewModel => (DialogLineModel)DataContext;

    public event EventHandler? TimelineRequested;

    private void DialogLine_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        TimelineRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CheckLineExpander()
    {
        Dispatcher.Invoke(() =>
        {
            PanelSeparator.Visibility = ViewModel.UseSeparator ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void DialogLine_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
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

        var token = CancellationToken.None;
        var dialogResult = await dialogService.ShowAsync(dialog, token);
        if (dialogResult != ContentDialogResult.Primary) return;

        var set = ViewModel.Set;
        var edited = dialog.ViewModel.ContentTranslated;
        ViewModel.TranslatedContent = dialog.ViewModel.ContentTranslated;

        DataContext = new DialogLineModel(set, CharTime);
        ViewModel.UseSeparator = dialog.ViewModel.UseReturn;
        if (edited.Contains('\n'))
        {
            var parts = edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }

        CheckLineExpander();
    }

}
