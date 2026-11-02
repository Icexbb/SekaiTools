using System.Windows;
using System.Windows.Controls;
using SekaiToolsGUI.ViewModel.Translate;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineActions : UserControl
{
    public TranslateLineActions() => InitializeComponent();

    public static readonly RoutedEvent VoiceRequestedEvent = EventManager.RegisterRoutedEvent(
        "VoiceRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TranslateLineActions));

    private void PlayVoice(object sender, RoutedEventArgs e)
    {
        if (DataContext is LineDialogModel { HasVoice: true } line)
            RaiseEvent(new RoutedEventArgs(VoiceRequestedEvent, line));
    }

    private void OpenActions(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void ApplyAction(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string action, DataContext: LineModel line }) line.RequestAction(action);
    }

    private void AdoptReference(object sender, RoutedEventArgs e)
    {
        if (DataContext is LineModel line) line.RequestAction("reference");
    }
}
