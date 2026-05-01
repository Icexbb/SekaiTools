namespace SekaiToolsMauiText.View.Translate;

public class LineModelTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DialogTemplate { get; set; }
    public DataTemplate? EffectTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item is ViewModel.LineDialogModel ? DialogTemplate! : EffectTemplate!;
    }
}

