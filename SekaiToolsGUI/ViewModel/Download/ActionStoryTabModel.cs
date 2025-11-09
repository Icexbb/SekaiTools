using SekaiDataFetch.Item;
using SekaiToolsBase.DataList;

namespace SekaiToolsGUI.ViewModel.Download;

public class ActionStoryTabModel : ViewModelBase
{
    public bool CheckBoxChara01
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara02
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara03
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara04
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara05
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara06
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara07
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara08
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara09
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara10
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara11
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara12
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara13
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara14
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara15
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara16
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara17
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara18
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara19
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara20
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara21
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara22
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara23
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara24
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara25
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara26
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara27
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara28
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara29
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara30
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public bool CheckBoxChara31
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public Area[] Areas
    {
        get => GetProperty<Area[]>([]);
        set => SetProperty(value);
    }

    public AreaStorySet[] EventStories
    {
        get => GetProperty<AreaStorySet[]>([]);
        set => SetProperty(value);
    }

    public bool AllChecked => CheckBoxChara01 && CheckBoxChara02 && CheckBoxChara03 && CheckBoxChara04 &&
                              CheckBoxChara05 && CheckBoxChara06 && CheckBoxChara07 && CheckBoxChara08 &&
                              CheckBoxChara09 && CheckBoxChara10 && CheckBoxChara11 && CheckBoxChara12 &&
                              CheckBoxChara13 && CheckBoxChara14 && CheckBoxChara15 && CheckBoxChara16 &&
                              CheckBoxChara17 && CheckBoxChara18 && CheckBoxChara19 && CheckBoxChara20 &&
                              CheckBoxChara21 && CheckBoxChara22 && CheckBoxChara23 && CheckBoxChara24 &&
                              CheckBoxChara25 && CheckBoxChara26 && CheckBoxChara27 && CheckBoxChara28 &&
                              CheckBoxChara29 && CheckBoxChara30 && CheckBoxChara31;

    public bool AllUnchecked => !CheckBoxChara01 && !CheckBoxChara02 && !CheckBoxChara03 && !CheckBoxChara04 &&
                                !CheckBoxChara05 && !CheckBoxChara06 && !CheckBoxChara07 && !CheckBoxChara08 &&
                                !CheckBoxChara09 && !CheckBoxChara10 && !CheckBoxChara11 && !CheckBoxChara12 &&
                                !CheckBoxChara13 && !CheckBoxChara14 && !CheckBoxChara15 && !CheckBoxChara16 &&
                                !CheckBoxChara17 && !CheckBoxChara18 && !CheckBoxChara19 && !CheckBoxChara20 &&
                                !CheckBoxChara21 && !CheckBoxChara22 && !CheckBoxChara23 && !CheckBoxChara24 &&
                                !CheckBoxChara25 && !CheckBoxChara26 && !CheckBoxChara27 && !CheckBoxChara28 &&
                                !CheckBoxChara29 && !CheckBoxChara30 && !CheckBoxChara31;
}