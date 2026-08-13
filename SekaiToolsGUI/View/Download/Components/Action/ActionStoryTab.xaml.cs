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


    public async Task Refresh()
    {
        CardCharacters.IsEnabled = false;
        CardUnits.IsEnabled = false;
        try
        {
            ActionStory.SetSource(GetSourceType());
            ActionStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await ActionStory.Refresh();
            InitializeAreas();
            RefreshItems();
        }
        finally
        {
            CardCharacters.IsEnabled = true;
            CardUnits.IsEnabled = true;
        }
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
        ViewModel.Areas = ActionStory.Areas.ToArray();
        BoxType.SelectedIndex = 0;
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
        if (ActionStory.Data.Count == 0) return;

        var data = ActionStory.Data.Select(item => (AreaStorySet)item.Clone()).ToList();
        data.Sort((x, y) => _currentDirection * x.ActionSet.Id.CompareTo(y.ActionSet.Id));
        ViewModel.EventStories = data.Where(JudgeVisibility).ToArray();
    }

    private bool JudgeVisibility(AreaStorySet data)
    {
        if (BoxType.SelectedItem is not Area selectedArea || selectedArea.Id != data.ActionSet.AreaId)
            return false;

        return CharacterComboBox.SelectedItem is not CharacterComboBoxItem character || character.Value == 0 ||
               data.CharacterIds.Contains(character.Value);
    }
}
