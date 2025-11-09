using System.Windows.Controls;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Translate;
using Wpf.Ui.Abstractions.Controls;

namespace SekaiToolsGUI.View.Translate.Components;

public partial class TranslateLineEffect : UserControl, INavigableView<LineEffectModel>, IExportable
{
    public TranslateLineEffect(BaseStoryEvent eBaseStoryEvent)
    {
        DataContext = new LineEffectModel(eBaseStoryEvent);
        InitializeComponent();
    }

    public string Export()
    {
        var result = ViewModel.TranslatedContent;
        return string.IsNullOrWhiteSpace(result) ? "地点" : result;
    }

    public LineEffectModel ViewModel => (LineEffectModel)DataContext;
}