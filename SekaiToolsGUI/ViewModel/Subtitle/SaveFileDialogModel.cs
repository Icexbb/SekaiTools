using System.IO;
using System.Text.RegularExpressions;
using SekaiToolsCore.Process.Config;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class SaveFileDialogModel : ViewModelBase
{
    private string? _confirmedOverwritePath;

    public bool NeedsOverwriteConfirmation => File.Exists(FileName) &&
        !string.Equals(_confirmedOverwritePath, FileName, StringComparison.OrdinalIgnoreCase);

    public void SetSelectedPath(string path, bool overwriteConfirmed)
    {
        FileName = path;
        _confirmedOverwritePath = overwriteConfirmed ? path : null;
    }

    public void ApplyTranslationTag(string tag)
    {
        if (tag is not ("翻译" or "校对" or "合意"))
            throw new ArgumentOutOfRangeException(nameof(tag));
        var name = Path.GetFileName(FileName);
        var prefix = Regex.Match(name, "^【[^】]*】");
        FileName = Path.Combine(Path.GetDirectoryName(FileName) ?? "",
            $"【{tag}】" + (prefix.Success ? name[prefix.Length..] : name));
        // Clicking a tag always invalidates consent, even when the path is unchanged.
        _confirmedOverwritePath = null;
    }

    public void LoadExportOptions(ExportStyleConfig config)
    {
        ExportLine1 = config.ExportLine1;
        ExportLine2 = config.ExportLine2;
        ExportLine3 = config.ExportLine3;
        ExportUnchangedCharacter = config.ExportUnchangedCharacter;
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
        ExportUnchangedCharacter = ExportUnchangedCharacter,
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

    public bool ExportUnchangedCharacter
    {
        get => GetProperty(false);
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
        set
        {
            if (!string.Equals(FileName, value, StringComparison.Ordinal))
                _confirmedOverwritePath = null;
            SetProperty(value);
        }
    }
}
