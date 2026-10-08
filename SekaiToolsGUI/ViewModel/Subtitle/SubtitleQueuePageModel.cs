using SekaiToolsGUI.Service;
using SekaiToolsGUI.View.Subtitle.Components;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public sealed class SubtitleQueuePageModel(SequentialTaskQueue<SubtitleQueueTaskModel> queue) : ViewModelBase
{
    public SequentialTaskQueue<SubtitleQueueTaskModel> Queue { get; } = queue;
    public bool ResourcesReady { get => GetProperty(false); set { SetProperty(value); OnPropertyChanged(nameof(CanOpenDialog)); } }
    public bool IsDialogOpen { get => GetProperty(false); set { SetProperty(value); OnPropertyChanged(nameof(CanOpenDialog)); } }
    public bool CanOpenDialog => ResourcesReady && !IsDialogOpen;
    public bool HasSelection => SelectedTask != null;
    public SubtitleTask? SelectedControl => SelectedTask?.Control;

    public SubtitleQueueTaskModel? SelectedTask
    {
        get => GetProperty<SubtitleQueueTaskModel?>();
        set
        {
            if (ReferenceEquals(SelectedTask, value)) return;
            if (SelectedTask != null) SelectedTask.IsSelected = false;
            SetProperty(value);
            if (value != null) value.IsSelected = true;
            OnPropertyChanged(nameof(SelectedControl));
            OnPropertyChanged(nameof(HasSelection));
        }
    }
}
