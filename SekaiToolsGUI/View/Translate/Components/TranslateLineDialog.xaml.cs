using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineDialog : UserControl, INavigableView<LineDialogModel>
{
    public static readonly DependencyProperty LineDialogModelProperty = DependencyProperty.Register(
        nameof(LineDialogModel), typeof(LineDialogModel), typeof(TranslateLineDialog),
        new PropertyMetadata(null, OnLineDialogModelChanged));

    public TranslateLineDialog()
    {
        InitializeComponent();
    }

    public LineDialogModel LineDialogModel
    {
        get => (LineDialogModel)GetValue(LineDialogModelProperty);
        set => SetValue(LineDialogModelProperty, value);
    }


    public LineDialogModel ViewModel => (LineDialogModel)DataContext;

    private static void OnLineDialogModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TranslateLineDialog control && e.NewValue is LineDialogModel lineDialogModel)
            control.DataContext = lineDialogModel;
    }
}

public partial class TranslateLineDialog : UserControl, INavigableView<LineDialogModel>
{
    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox box && e.Key == Key.Enter &&
            (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            e.Handled = !ViewModel.CanReplaceText(box.Text, box.SelectionStart, box.SelectionLength, "\n");
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox box)
            e.Handled = !ViewModel.CanReplaceText(box.Text, box.SelectionStart, box.SelectionLength, e.Text);
    }

    private void TextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is TextBox box && e.DataObject.GetData(DataFormats.UnicodeText) is string text &&
            !ViewModel.CanReplaceText(box.Text, box.SelectionStart, box.SelectionLength, text)) e.CancelCommand();
    }

    private void TextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel.DeferFormatting = true;
    private void TextBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ViewModel.CompleteTextEdit();

    private void NameEditBoxChanged(object sender, TextChangedEventArgs e) { }
}
