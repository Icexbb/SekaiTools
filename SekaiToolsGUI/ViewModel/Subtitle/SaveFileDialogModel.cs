using SekaiToolsCore.Process.Config;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class SaveFileDialogModel : ViewModelBase
{
    public void LoadExportOptions(ExportStyleConfig config)
    {
        ExportLine1 = config.ExportLine1;
        ExportLine2 = config.ExportLine2;
        ExportLine3 = config.ExportLine3;
        ExportCharacter = config.ExportCharacter;
        ExportBannerMask = config.ExportBannerMask;
        ExportBannerText = config.ExportBannerText;
        ExportMarkerMask = config.ExportMarkerMask;
        ExportMarkerText = config.ExportMarkerText;
        ExportScreenComment = config.ExportScreenComment;
    }

    public ExportStyleConfig GetExportStyleConfig() => new()
    {
        ExportLine1 = ExportLine1,
        ExportLine2 = ExportLine2,
        ExportLine3 = ExportLine3,
        ExportCharacter = ExportCharacter,
        ExportBannerMask = ExportBannerMask,
        ExportBannerText = ExportBannerText,
        ExportMarkerMask = ExportMarkerMask,
        ExportMarkerText = ExportMarkerText,
        ExportScreenComment = ExportScreenComment,
    };

    public bool ExportLine1
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine2
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportLine3
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportCharacter
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportBannerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerMask
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportMarkerText
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

    public bool ExportScreenComment
    {
        get => GetProperty(true);
        set => SetProperty(value);
    }

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