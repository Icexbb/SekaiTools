using System.Windows.Controls;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Abstractions.Controls;
using SekaiEvent = SekaiToolsCore.Story.Event.Event;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineEffect : UserControl, INavigableView<LineEffectModel>, IExportable
{
    public TranslateLineEffect(SekaiEvent eEvent)
    {
        DataContext = new LineEffectModel(eEvent);
        InitializeComponent();
    }

    public string Export()
    {
        var result = ViewModel.TranslatedContent;
        return string.IsNullOrWhiteSpace(result) ? "地点" : result;
    }

    public LineEffectModel ViewModel => (LineEffectModel)DataContext;
}