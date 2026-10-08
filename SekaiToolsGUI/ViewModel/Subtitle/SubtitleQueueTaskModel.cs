using System.ComponentModel;
using SekaiToolsCore;
using SekaiToolsCore.Process;
using SekaiToolsGUI.View.Subtitle.Components;

namespace SekaiToolsGUI.ViewModel.Subtitle;

public sealed class SubtitleQueueTaskModel : ViewModelBase
{
    private SubtitleTask? _control;

    public SubtitleQueueTaskModel(SubtitlePageModel state, bool isHistory = false, ProcessingState? savedState = null)
    {
        State = state;
        IsHistory = isHistory;
        SavedState = savedState;
        State.PropertyChanged += StateChanged;
    }

    public SubtitlePageModel State { get; }
    public bool IsHistory { get; }
    public ProcessingState? SavedState { get; }
    public SubtitleTask Control => _control ??= new SubtitleTask(State);
    internal SubtitleTask? CreatedControl => _control;
    public string VideoName => State.VideoFileName;
    public string Status => IsHistory ? $"历史 · {State.RunningStatus}" : State.RunningStatus;
    public bool CanReorder => !IsHistory && State.HasNotStarted;
    public bool IsSelected { get => GetProperty(false); set => SetProperty(value); }

    private void StateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SubtitlePageModel.VideoFileName)) OnPropertyChanged(nameof(VideoName));
        if (args.PropertyName == nameof(SubtitlePageModel.RunningStatus)) OnPropertyChanged(nameof(Status));
        if (args.PropertyName == nameof(SubtitlePageModel.HasNotStarted)) OnPropertyChanged(nameof(CanReorder));
    }

    internal void Detach() => State.PropertyChanged -= StateChanged;
}
