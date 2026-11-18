using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SekaiToolsGUI.Control;

/// <summary>Resizable editing area with an auxiliary pane that folds at the shared narrow breakpoint.</summary>
public partial class WorkbenchSplitView : UserControl
{
    private bool? _compact;
    private GridLength _primaryWidth = new(2, GridUnitType.Star);
    private GridLength _auxiliaryWidth = new(1, GridUnitType.Star);

    public WorkbenchSplitView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateCompact(AdaptiveLayout.GetIsNarrow(this));
    }

    public static readonly DependencyProperty PrimaryContentProperty = DependencyProperty.Register(
        nameof(PrimaryContent), typeof(object), typeof(WorkbenchSplitView));
    public static readonly DependencyProperty AuxiliaryContentProperty = DependencyProperty.Register(
        nameof(AuxiliaryContent), typeof(object), typeof(WorkbenchSplitView));

    public object? PrimaryContent { get => GetValue(PrimaryContentProperty); set => SetValue(PrimaryContentProperty, value); }
    public object? AuxiliaryContent { get => GetValue(AuxiliaryContentProperty); set => SetValue(AuxiliaryContentProperty, value); }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == AdaptiveLayout.IsNarrowProperty && PrimaryColumn is not null)
            UpdateCompact((bool)e.NewValue);
    }

    private void UpdateCompact(bool compact)
    {
        if (_compact == compact) return;
        if (compact)
        {
            _primaryWidth = PrimaryColumn.Width;
            _auxiliaryWidth = AuxiliaryColumn.Width;
            if (AuxiliaryPresenter.IsKeyboardFocusWithin)
                PrimaryPresenter.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            AuxiliaryPresenter.Visibility = Divider.Visibility = Visibility.Collapsed;
            AuxiliaryColumn.MinWidth = 0;
            AuxiliaryColumn.Width = DividerColumn.Width = new GridLength(0);
            PrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
            if (IsKeyboardFocusWithin)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PrimaryPresenter.BringIntoView));
        }
        else
        {
            PrimaryColumn.Width = _primaryWidth;
            AuxiliaryColumn.MinWidth = 240;
            AuxiliaryColumn.Width = _auxiliaryWidth;
            DividerColumn.Width = new GridLength(8);
            AuxiliaryPresenter.Visibility = Divider.Visibility = Visibility.Visible;
        }
        _compact = compact;
    }
}
