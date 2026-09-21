namespace SekaiToolsGUI.ViewModel.Suppress;

public class SuppressPageModel : ViewModelBase
{
    public SuppressPageModel()
    {
        PendingJobs.CollectionChanged += JobsChanged;
        CompletedJobs.CollectionChanged += JobsChanged;
    }

    private void JobChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not SuppressionJobModel job || e.PropertyName != nameof(SuppressionJobModel.Progress)) return;

        if (job.IsFinished && (ReferenceEquals(RunningJob, job) || PendingJobs.Contains(job)))
            MoveToCompleted(job);
        else
            RefreshSummary();
    }

    private void JobsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (SuppressionJobModel job in e.OldItems) job.PropertyChanged -= JobChanged;
        if (e.NewItems != null)
            foreach (SuppressionJobModel job in e.NewItems) job.PropertyChanged += JobChanged;
        RefreshStartStates();
        RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(CanClear));
    }

    private void RefreshStartStates()
    {
        foreach (var job in PendingJobs)
            job.CanStart = !HasRunningJob && job.Progress.State == SekaiToolsMedia.VideoSuppressionState.Idle;
    }

    public bool CanClear => CompletedJobs.Count > 0;
    public bool HasRunningJob => RunningJob != null;
    public string QueueSummary => $"共 {PendingJobs.Count + CompletedJobs.Count + (HasRunningJob ? 1 : 0)} 项 · 等待 {PendingJobs.Count} · 处理中 {(HasRunningJob ? 1 : 0)} · 已结束 {CompletedJobs.Count}";

    public static SuppressPageModel Instance { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<SuppressionJobModel> PendingJobs { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<SuppressionJobModel> CompletedJobs { get; } = new();

    public SuppressionJobModel? RunningJob
    {
        get => GetProperty<SuppressionJobModel?>();
        private set
        {
            var old = GetProperty<SuppressionJobModel?>();
            if (ReferenceEquals(old, value)) return;
            if (old != null) old.PropertyChanged -= JobChanged;
            SetProperty(value);
            if (value != null) value.PropertyChanged += JobChanged;
            OnPropertyChanged(nameof(HasRunningJob));
            RefreshStartStates();
            RefreshSummary();
        }
    }

    public bool AutoRunQueue
    {
        get => GetProperty(false);
        set => SetProperty(value);
    }

    public void AddPending(SuppressionJobModel job) => PendingJobs.Add(job);

    public bool TryStart(SuppressionJobModel job)
    {
        if (HasRunningJob || job.Progress.State != SekaiToolsMedia.VideoSuppressionState.Idle || !PendingJobs.Remove(job))
            return false;

        RunningJob = job;
        return true;
    }

    public void RestorePending(SuppressionJobModel job)
    {
        if (ReferenceEquals(RunningJob, job)) RunningJob = null;
        if (!PendingJobs.Contains(job)) PendingJobs.Insert(0, job);
    }

    private void MoveToCompleted(SuppressionJobModel job)
    {
        job.CanStart = false;
        if (ReferenceEquals(RunningJob, job)) RunningJob = null;
        PendingJobs.Remove(job);
        if (!CompletedJobs.Contains(job)) CompletedJobs.Add(job);
        RefreshSummary();
    }

    public void ClearCompleted() => CompletedJobs.Clear();

    public bool IsPreparingResources
    {
        get => GetProperty(false);
        private set => SetProperty(value);
    }

    public bool ResourcesReady
    {
        get => GetProperty(false);
        private set => SetProperty(value);
    }

    public string ResourcePreparationError
    {
        get => GetProperty("");
        private set => SetProperty(value);
    }


    public void BeginResourcePreparation()
    {
        IsPreparingResources = true;
        ResourcesReady = false;
        ResourcePreparationError = "";
    }

    public void CompleteResourcePreparation()
    {
        IsPreparingResources = false;
        ResourcesReady = true;
        ResourcePreparationError = "";
    }

    public void FailResourcePreparation()
    {
        IsPreparingResources = false;
        ResourcesReady = false;
        ResourcePreparationError = "视频压制环境准备失败，请重试或检查设置";
    }

}
