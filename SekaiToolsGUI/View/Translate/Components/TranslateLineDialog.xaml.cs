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
        if (sender is TextBox textBox)
        {
            if (e.Key != Key.Enter) return;
            var lineCount = textBox.LineCount;
            if (lineCount >= 3) e.Handled = true; // 阻止回车键输入新行
        }

        if (sender is Wpf.Ui.Controls.TextBox uiTextBox)
        {
            if (e.Key != Key.Enter) return;
            var lineCount = uiTextBox.LineCount;
            if (lineCount >= 3) e.Handled = true; // 阻止回车键输入新行
        }
    }

    private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            var newLineCount = newText.Split('\n').Length;

            if (newLineCount > 3) e.Handled = true; // 阻止输入导致超过三行
        }

        if (sender is Wpf.Ui.Controls.TextBox uiTextBox)
        {
            var newText = uiTextBox.Text.Insert(uiTextBox.CaretIndex, e.Text);
            var newLineCount = newText.Split('\n').Length;

            if (newLineCount > 3) e.Handled = true; // 阻止输入导致超过三行
        }
    }

    private void NameEditBoxChanged(object sender, TextChangedEventArgs e)
    {
    }
}