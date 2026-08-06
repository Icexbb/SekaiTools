using System.Windows.Media;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public class TimelineEditorModel : ViewModelBase
{
    public bool ShowTimeLine
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public string EventNumber
    {
        get => GetProperty("未选择");
        set => SetProperty(value);
    }

    public string EventType
    {
        get => GetProperty("事件");
        set => SetProperty(value);
    }

    public string EventContent
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public Brush EventAccentBrush
    {
        get => GetProperty<Brush>(Brushes.Gray);
        set => SetProperty(value);
    }

    public bool HasSelection
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            RefreshEditState();
        }
    }

    public bool IsReadOnly
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            RefreshEditState();
            RefreshUndoState();
        }
    }

    public bool HasTimingEdits
    {
        get => GetProperty(false);
        set
        {
            SetProperty(value);
            OnPropertyChanged(nameof(CanRestoreTiming));
        }
    }

    public string StartTime
    {
        get => GetProperty("--:--:--.--");
        set => SetProperty(value);
    }

    public string EndTime
    {
        get => GetProperty("--:--:--.--");
        set => SetProperty(value);
    }

    public string Duration
    {
        get => GetProperty("--");
        set => SetProperty(value);
    }

    public int UndoCount
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            RefreshUndoState();
        }
    }

    public bool CanEdit => !IsReadOnly && HasSelection;
    public bool CanRestoreTiming => CanEdit && HasTimingEdits;
    public bool CanUndo => !IsReadOnly && UndoCount > 0;

    public string UndoToolTip => UndoCount == 0
        ? "没有可撤回的时间修改"
        : $"撤回时间修改 (Ctrl+Z) · {UndoCount}";

    public string ZoomText
    {
        get => GetProperty("100px/s");
        set => SetProperty(value);
    }

    public string WaveformStatus
    {
        get => GetProperty("");
        set => SetProperty(value);
    }

    public void SetSelection(
        string eventNumber,
        string eventType,
        string eventContent,
        Brush accentBrush,
        bool hasTimingEdits)
    {
        EventNumber = eventNumber;
        EventType = eventType;
        EventContent = eventContent;
        EventAccentBrush = accentBrush;
        HasTimingEdits = hasTimingEdits;
        HasSelection = true;
    }

    public void ClearSelection()
    {
        EventNumber = "未选择";
        EventType = "事件";
        EventContent = "";
        EventAccentBrush = Brushes.Gray;
        HasTimingEdits = false;
        HasSelection = false;
        StartTime = "--:--:--.--";
        EndTime = "--:--:--.--";
        Duration = "--";
        UndoCount = 0;
        WaveformStatus = "";
    }

    private void RefreshEditState()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanRestoreTiming));
    }

    private void RefreshUndoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoToolTip));
    }
}
