using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using SekaiToolsMedia;

namespace SekaiToolsGUI.ViewModel.Suppress;

public class SuppressPageModel : ViewModelBase
{
    public SuppressPageModel()
    {
        FilteredCompletedJobs = new ListCollectionView(CompletedJobs);
        FilteredCompletedJobs.Filter = value => value is SuppressionJobModel job && (CompletedFilter == 0 ||
            job.Progress.State == (CompletedFilter == 1 ? VideoSuppressionState.Completed :
                CompletedFilter == 2 ? VideoSuppressionState.Failed : VideoSuppressionState.Cancelled));
        PendingJobs.CollectionChanged += JobsChanged;
        CompletedJobs.CollectionChanged += JobsChanged;
    }

    public ListCollectionView FilteredCompletedJobs { get; }

    public int CompletedFilter
    {
        get => GetProperty(0);
        set
        {
            SetProperty(value);
            FilteredCompletedJobs.Refresh();
        }
    }

    public bool CanClear => CompletedJobs.Count > 0;
    public bool HasRunningJob => RunningJob != null;

    public string QueueSummary =>
        $"共 {PendingJobs.Count + CompletedJobs.Count + (HasRunningJob ? 1 : 0)} 项 · 等待 {PendingJobs.Count} · 处理中 {(HasRunningJob ? 1 : 0)} · 已结束 {CompletedJobs.Count}";

    public static SuppressPageModel Instance { get; } = new();
    public ObservableCollection<SuppressionJobModel> PendingJobs { get; } = new();
    public ObservableCollection<SuppressionJobModel> CompletedJobs { get; } = new();

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
            OnPropertyChanged(nameof(RunningJobs));
            RefreshStartStates();
            RefreshSummary();
        }
    }

    public SuppressionJobModel? SelectedJob
    {
        get => GetProperty<SuppressionJobModel?>();
        set
        {
            if (SelectedJob is { } previous) previous.IsSelected = false;
            SetProperty(value);
            if (value != null) value.IsSelected = true;
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedJob != null;
    public IEnumerable<SuppressionJobModel> RunningJobs => RunningJob == null ? [] : [RunningJob];

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

    private void JobChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SuppressionJobModel job || e.PropertyName != nameof(SuppressionJobModel.Progress)) return;

        if (job.IsFinished && (ReferenceEquals(RunningJob, job) || PendingJobs.Contains(job)))
            MoveToCompleted(job);
        else
            RefreshSummary();
    }

    private void JobsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (SuppressionJobModel job in e.OldItems)
                job.PropertyChanged -= JobChanged;
        if (e.NewItems != null)
            foreach (SuppressionJobModel job in e.NewItems)
                job.PropertyChanged += JobChanged;
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
            job.CanStart = !job.IsEditing && !HasRunningJob && job.Progress.State == VideoSuppressionState.Idle;
    }

    public void RemoveJob(SuppressionJobModel job)
    {
        if (ReferenceEquals(RunningJob, job) || job.IsEditing) return;
        PendingJobs.Remove(job);
        CompletedJobs.Remove(job);
        job.Job.Cancel();
        if (ReferenceEquals(SelectedJob, job))
            SelectedJob = RunningJob ?? PendingJobs.FirstOrDefault() ?? CompletedJobs.LastOrDefault();
    }

    public bool BeginEdit(SuppressionJobModel job)
    {
        if (job.IsEditing || ReferenceEquals(RunningJob, job)) return false;
        if (!PendingJobs.Contains(job) && !CompletedJobs.Contains(job)) return false;
        job.IsEditing = true;
        RefreshStartStates();
        return true;
    }

    public void EndEdit(SuppressionJobModel job)
    {
        job.IsEditing = false;
        RefreshStartStates();
    }

    public void CommitPending(SuppressionJobModel replacement, SuppressionJobModel? original = null)
    {
        replacement.Job.Options.Validate();
        var output = Path.GetFullPath(replacement.OutputPath);
        if (PendingJobs.Any(x =>
                !ReferenceEquals(x, original) && string.Equals(Path.GetFullPath(x.OutputPath), output,
                    StringComparison.OrdinalIgnoreCase)) ||
            (RunningJob != null && string.Equals(Path.GetFullPath(RunningJob.OutputPath), output,
                StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("队列中已有任务使用此输出路径，请选择其他路径。");
        if (original != null)
        {
            if (!original.IsEditing || ReferenceEquals(RunningJob, original))
                throw new InvalidOperationException("任务状态已改变，请重新打开编辑窗口。");
            var index = PendingJobs.IndexOf(original);
            if (index >= 0)
            {
                PendingJobs[index] = replacement;
                if (ReferenceEquals(SelectedJob, original)) SelectedJob = replacement;
                return;
            }

            if (!CompletedJobs.Contains(original)) throw new InvalidOperationException("原任务已移除，请重新创建任务。");
        }

        PendingJobs.Add(replacement);
    }

    public void AddPending(SuppressionJobModel job)
    {
        PendingJobs.Add(job);
    }

    public bool TryStart(SuppressionJobModel job)
    {
        if (job.IsEditing || HasRunningJob || job.Progress.State != VideoSuppressionState.Idle ||
            !PendingJobs.Remove(job))
            return false;

        RunningJob = job;
        job.Progress = job.Progress with { State = VideoSuppressionState.Preparing };
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

    public void ClearCompleted()
    {
        var clearSelection = SelectedJob != null && CompletedJobs.Contains(SelectedJob);
        CompletedJobs.Clear();
        if (clearSelection) SelectedJob = RunningJob ?? PendingJobs.FirstOrDefault();
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