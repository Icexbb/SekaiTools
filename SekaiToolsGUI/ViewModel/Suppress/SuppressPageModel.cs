namespace SekaiToolsGUI.ViewModel.Suppress;

public class SuppressPageModel : ViewModelBase
{
    public SuppressPageModel()
    {
        Jobs.CollectionChanged += (_, e) =>
        {
            if (e.OldItems != null)
                foreach (SuppressionJobModel job in e.OldItems) job.PropertyChanged -= JobChanged;
            if (e.NewItems != null)
                foreach (SuppressionJobModel job in e.NewItems) job.PropertyChanged += JobChanged;
            RefreshSummary();
        };
    }

    private void JobChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SuppressionJobModel.Progress)) RefreshSummary();
    }

    private void RefreshSummary()
    {
        OnPropertyChanged(nameof(QueueSummary));
        OnPropertyChanged(nameof(CanClear));
    }

    public bool CanClear => Jobs.Any(x => x.IsFinished);
    public string QueueSummary => $"共 {Jobs.Count} 项 · 等待 {Jobs.Count(x => x.Progress.State == SekaiToolsMedia.VideoSuppressionState.Idle)} · 处理中 {Jobs.Count(x => x.Progress.Running)} · 已结束 {Jobs.Count(x => x.IsFinished)}";

    public static SuppressPageModel Instance { get; } = new();
    public System.Collections.ObjectModel.ObservableCollection<SuppressionJobModel> Jobs { get; } = new();

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
