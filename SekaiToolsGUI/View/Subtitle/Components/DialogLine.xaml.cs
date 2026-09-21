using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SekaiToolsCore.Process.FrameSet;
using SekaiToolsGUI.ViewModel.Setting;
using SekaiToolsGUI.ViewModel.Subtitle;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SekaiToolsGUI.View.Subtitle.Components;

public partial class DialogLine : UserControl, INavigableView<DialogLineModel>
{
    public DialogLine(DialogBaseFrameSet set)
    {
        set.InitSeparator();
        DataContext = new DialogLineModel(set, CharTime);
        InitializeComponent();
    }

    private int CharTime => SettingPageModel.Instance.TypewriterCharTime;

    public DialogLineModel ViewModel => (DialogLineModel)DataContext;

    public event EventHandler? TimelineRequested;

    public void RefreshTiming()
    {
        ViewModel.RefreshTiming();
        SeparateFrameSlider.GetBindingExpression(Slider.ValueProperty)?.UpdateTarget();
        BindingOperations
            .GetMultiBindingExpression(SeparateTimeText, TextBlock.TextProperty)
            ?.UpdateTarget();
    }

    private void DialogLine_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        TimelineRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CheckLineExpander()
    {
        Dispatcher.Invoke(() =>
        {
            PanelSeparator.Visibility = ViewModel.SeparatorEnabled ? Visibility.Visible : Visibility.Collapsed;
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
        if (edited.Contains('\n'))
        {
            var parts = edited.Split('\n');
            ViewModel.SeparatorContentIndex = parts[0].Length;
        }
    }

    private void SeparateBtn_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SeparatorEnabled = !ViewModel.SeparatorEnabled;
    }
}