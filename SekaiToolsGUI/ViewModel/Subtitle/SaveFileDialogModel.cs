namespace SekaiToolsGUI.ViewModel;

public class SaveFileDialogModel : ViewModelBase
{
    public bool UseStaff
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public double StaffLineTime
    {
        get => GetProperty(5.0);
        set => SetProperty(value);
    }

    public string StaffLinePrefix
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineSuffix
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineRecord
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineTranslator
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineTranslatorSenior
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineTimeline
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineTimelineSenior
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public string StaffLineCompression
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public int StaffLinePositionIndex
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            StaffLinePosition = value switch
            {
                0 => 1,
                1 => 7,
                2 => 3,
                3 => 9,
                _ => 1
            };
        }
    }

    public int StaffLinePosition
    {
        get => GetProperty(1);
        set => SetProperty(value);
    }

    public string FileName
    {
        get => GetProperty("");
        set => SetProperty(value);
    }
}