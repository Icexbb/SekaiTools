using System.Runtime.CompilerServices;
using SekaiDataFetch.Source;

namespace SekaiToolsGUI.ViewModel;

public class DownloadSourceEditorModel : ViewModelBase
{
    public string SourceName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string SourceTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StorageBaseUrl
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string ActionSetTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string MemberStoryTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string EventStoryTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string SpecialStoryTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string UnitStoryTemplate
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public bool Deletable
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    private new void SetProperty<T>(T value, [CallerMemberName] string? propertyName = null)
    {
        base.SetProperty(value, propertyName);
        if (Initialized) Parent.SaveSource();
    }

    private bool Initialized { get; }
    private SettingPageModel Parent { get; }

    public SourceData Data => new()
    {
        SourceName = SourceName,
        StorageBaseUrl = StorageBaseUrl,
        SourceTemplate = SourceTemplate,
        ActionSetTemplate = ActionSetTemplate,
        MemberStoryTemplate = MemberStoryTemplate,
        EventStoryTemplate = EventStoryTemplate,
        SpecialStoryTemplate = SpecialStoryTemplate,
        UnitStoryTemplate = UnitStoryTemplate,
        Deletable = Deletable
    };

    public DownloadSourceEditorModel(SettingPageModel parent, SourceData source)
    {
        Initialized = false;
        Parent = parent;
        SourceName = source.SourceName;
        SourceTemplate = source.SourceTemplate;
        StorageBaseUrl = source.StorageBaseUrl;
        ActionSetTemplate = source.ActionSetTemplate;
        MemberStoryTemplate = source.MemberStoryTemplate;
        EventStoryTemplate = source.EventStoryTemplate;
        SpecialStoryTemplate = source.SpecialStoryTemplate;
        UnitStoryTemplate = source.UnitStoryTemplate;
        Deletable = source.Deletable;
        Initialized = true;
    }

    public void Delete()
    {
        Parent.DeleteSource(this);
    }
}