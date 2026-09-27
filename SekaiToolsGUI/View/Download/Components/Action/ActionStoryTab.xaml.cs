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
            (CharacterComboBox.SelectedItem as CharacterComboBoxItem)?.Value ?? 0, minimum, maximum);
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
