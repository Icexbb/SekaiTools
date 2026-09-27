using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsBase.DataList;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Action;

public partial class ActionStoryTab : UserControl, IRefreshable
{
    private int _currentDirection = 1;
    private bool _updatingFilterValues;

    public ActionStoryTab()
    {
        DataContext ??= new ActionStoryTabModel();
        InitializeComponent();
        InitializeCharacterComboBox();
    }

    private ActionStoryTabModel ViewModel => (ActionStoryTabModel)DataContext;
    private ListActionStory ActionStory => ListActionStory.Instance;


    public async Task Refresh(IProgress<ListRefreshProgress>? progress = null)
    {
        ActionStory.SetSource(GetSourceType());
        ActionStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ActionStory.Refresh(progress);
        InitializeAreas();
        InitializeFilterValues();
        RefreshItems();
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }


    private void ActionStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeAreas();
        InitializeFilterValues();
        if (CharacterComboBox.SelectedIndex < 0) CharacterComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    public void InitializeAreas()
    {
        var selectedId = (BoxType.SelectedItem as AreaFilterOption)?.Id;
        ViewModel.Areas = [new(null, "全部地点"), .. ActionStory.Areas.Select(area => new AreaFilterOption(area.Id, area.AreaName))];
        BoxType.SelectedItem = ViewModel.Areas.FirstOrDefault(area => area.Id == selectedId) ?? ViewModel.Areas[0];
        RefreshItems();
    }

    private void InitializeCharacterComboBox()
    {
        var characters = CharacterFilterOptions.CreateItems();
        ViewModel.Characters = characters;
        var groupedView = CollectionViewSource.GetDefaultView(characters);
        groupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CharacterComboBoxItem.GroupName)));
        CharacterComboBox.ItemsSource = groupedView;
        CharacterComboBox.SelectedIndex = 0;
    }

    private void CharacterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void Filter_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void ButtonSort_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonSort.RenderTransform = new ScaleTransform(-1, _currentDirection);
        _currentDirection *= -1;
        RefreshItems();
    }

    private ActionStoryFilterMode FilterMode => ModeBox == null ? ActionStoryFilterMode.All : ModeBox.SelectedIndex switch
    {
        1 => ActionStoryFilterMode.ReleaseActivity,
        2 => ActionStoryFilterMode.AdditionActivity,
        3 => ActionStoryFilterMode.Type,
        4 => ActionStoryFilterMode.Batch,
        5 => ActionStoryFilterMode.ArchiveDate,
        _ => ActionStoryFilterMode.All
    };

    private void Mode_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        InitializeFilterValues(preserveSelection: false);
        RefreshItems();
    }

    private void Value_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFilterValues) RefreshItems();
    }

    private void InitializeFilterValues(bool preserveSelection = true)
    {
        if (ValueBox == null || FilterHint == null) return;
        var previous = preserveSelection ? (ValueBox.SelectedItem as ActionFilterOption)?.Value : null;
        var data = ActionStory.Data;
        IEnumerable<ActionFilterOption> values = FilterMode switch
        {
            ActionStoryFilterMode.ReleaseActivity => data.Select(story => story.ReleaseActivity).OfType<ActionStoryActivity>()
                .DistinctBy(activity => activity.Id).OrderByDescending(activity => activity.Number)
                .Select(activity => new ActionFilterOption(activity.Id.ToString(), activity.DisplayName))
                .Append(new("unknown", "无活动关联／未归类")),
            ActionStoryFilterMode.AdditionActivity => data.Select(story => story.AdditionActivity).OfType<ActionStoryActivity>()
                .DistinctBy(activity => activity.Id).OrderByDescending(activity => activity.Number)
                .Select(activity => new ActionFilterOption(activity.Id.ToString(), activity.DisplayName + " · 推断"))
                .Append(new("unknown", "初始／特殊批次／未归类")),
            ActionStoryFilterMode.Type => data.DistinctBy(story => story.ActionSet.ActionSetType)
                .OrderBy(story => story.ActionSet.ActionSetType)
                .Select(story => new ActionFilterOption(story.ActionSet.ActionSetType, story.TypeName)),
            ActionStoryFilterMode.Batch => data.DistinctBy(story => story.BatchKey).OrderByDescending(story => story.BatchKey)
                .Select(story => new ActionFilterOption(story.BatchKey, story.BatchName)),
            ActionStoryFilterMode.ArchiveDate => data.Select(story => story.ArchiveDate).OfType<string>()
                .Distinct().OrderDescending().Select(date => new ActionFilterOption(date, date))
                .Append(new("unknown", "未设置归档日期")),
            _ => []
        };
        var options = new[] { new ActionFilterOption(null, "全部") }.Concat(values).ToArray();
        _updatingFilterValues = true;
        try
        {
            ValueBox.ItemsSource = options;
            ValueBox.SelectedItem = options.FirstOrDefault(option => option.Value == previous) ?? options[0];
            ValueBox.Visibility = FilterMode == ActionStoryFilterMode.All ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            _updatingFilterValues = false;
        }
        FilterHint.Text = FilterMode switch
        {
            ActionStoryFilterMode.AdditionActivity => "追加活动按对话 ID 和已知解锁活动推断，可能与实际追加批次不同；特殊批次请使用“更新批次”。",
            ActionStoryFilterMode.ReleaseActivity => "按剧情章节解锁条件关联活动；期数按有剧情的活动顺序排列。",
            ActionStoryFilterMode.Batch => "每月、愚人节及周年按剧本命名归类，未知命名保留在“其他更新／未归类”。",
            ActionStoryFilterMode.ArchiveDate => "归档日期按 UTC+9 显示，不代表首次追加日期；原始数据可能含默认占位日期。",
            _ => "所有筛选条件取交集；ID 范围留空表示不限。"
        };
        if (!ActionStory.ActivityMetadataAvailable)
            FilterHint.Text += " 活动数据未就绪或不可用，可刷新列表重试；基础筛选仍可使用。";
    }
}

partial class ActionStoryTab
{
    private void RefreshItems()
    {
        if (MinimumIdBox == null || MaximumIdBox == null || ResultText == null || AddFilteredButton == null) return;
        var validMinimum = TryReadId(MinimumIdBox.Text, out var minimum);
        var validMaximum = TryReadId(MaximumIdBox.Text, out var maximum);
        var valid = validMinimum && validMaximum;
        valid = valid && (!minimum.HasValue || !maximum.HasValue || minimum <= maximum);
        if (!valid)
        {
            ViewModel.EventStories = [];
            ResultText.Text = "请输入正整数 ID，且起始 ID 不大于结束 ID";
            AddFilteredButton.IsEnabled = false;
            return;
        }

        var filter = new ActionStoryFilter((BoxType.SelectedItem as AreaFilterOption)?.Id,
            (CharacterComboBox.SelectedItem as CharacterComboBoxItem)?.Value ?? 0, minimum, maximum,
            FilterMode, (ValueBox?.SelectedItem as ActionFilterOption)?.Value);
        var data = ActionStory.Data.Where(filter.Matches);
        ViewModel.EventStories = (_currentDirection == 1
            ? data.OrderBy(item => item.ActionSet.Id)
            : data.OrderByDescending(item => item.ActionSet.Id)).ToArray();
        ResultText.Text = $"符合条件：{ViewModel.EventStories.Length} 条";
        AddFilteredButton.IsEnabled = ViewModel.EventStories.Length > 0;
    }

    private static bool TryReadId(string text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text, out var id) || id <= 0) return false;
        value = id;
        return true;
    }

    private void IdRange_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void AddFiltered_OnClick(object sender, RoutedEventArgs e)
    {
        DependencyObject? parent = this;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not DownloadPage page) return;
        SourceList.Instance.SourceData = page.GetSourceType();
        var sourceName = page.GetSourceType().SourceName;
        page.AddTasks(ViewModel.EventStories.Select(story =>
            ($"{sourceName}|ActionSet|{story.ActionSet.Id}-{story.ActionSet.ScriptId}", SourceList.Instance.ActionSet(story))));
    }
}
