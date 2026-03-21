using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineEffect : UserControl, INavigableView<LineEffectModel>
{
    public static readonly DependencyProperty LineEffectModelProperty = DependencyProperty.Register(
        nameof(LineEffectModel), typeof(LineEffectModel), typeof(TranslateLineEffect),
        new PropertyMetadata(null, OnLineEffectModelChanged));

    public TranslateLineEffect()
    {
        InitializeComponent();
    }

    public LineEffectModel? LineEffectModel
    {
        get => (LineEffectModel?)GetValue(LineEffectModelProperty);
        set => SetValue(LineEffectModelProperty, value);
    }


    public LineEffectModel ViewModel => (LineEffectModel)DataContext;

    private static void OnLineEffectModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TranslateLineEffect control && e.NewValue is LineEffectModel lineEffectModel)
            control.DataContext = lineEffectModel;
    }
}