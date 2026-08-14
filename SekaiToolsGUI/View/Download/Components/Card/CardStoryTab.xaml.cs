using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Card;

public partial class CardStoryTab : UserControl, IRefreshable
{
    public CardStoryTab()
    {
        DataContext ??= new CardStoryTabModel();
        InitializeComponent();
        InitializeCharacterComboBox();
    }

    private CardStoryTabModel ViewModel => (CardStoryTabModel)DataContext;
    private ListCardStory CardStory => ListCardStory.Instance;

    public async Task Refresh()
    {
        FilterContainer.IsEnabled = false;
        try
        {
            CardStory.SetSource(GetSourceType());
            CardStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await CardStory.Refresh();
            RefreshItems();
        }
        finally
        {
            FilterContainer.IsEnabled = true;
        }
    }

    private SourceData GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }

    private void InitializeCharacterComboBox()
    {
        var characters = CharacterFilterOptions.CreateItems(false);
        ViewModel.Characters = characters;
        var groupedView = CollectionViewSource.GetDefaultView(characters);
        groupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CharacterComboBoxItem.GroupName)));
        CharacterComboBox.ItemsSource = groupedView;
        CharacterComboBox.SelectedIndex = 0;
    }

    private void Filter_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void RefreshItems()
    {
        if (CardStory.Data.Count == 0) return;
        if (CharacterComboBox.SelectedIndex < 0) CharacterComboBox.SelectedIndex = 0;

        var characterId = CharacterComboBox.SelectedItem is CharacterComboBoxItem character
            ? character.Value
            : 0;
        var rarity = (RarityComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        var stories = CardStory.Data
            .Where(item => characterId == 0 || item.Card.CharacterId == characterId)
            .Where(item => string.IsNullOrEmpty(rarity) || item.Card.CardRarityType == rarity)
            .ToArray();

        ViewModel.CardStories = _currentDirection == -1 ? [.. stories.Reverse()] : stories;
    }

    private void CardStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        CharacterComboBox.SelectedIndex = 0;
        RarityComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    private int _currentDirection = -1;

    private void ButtonSort_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonSort.RenderTransform = new ScaleTransform(-1, _currentDirection);
        _currentDirection *= -1;
        RefreshItems();
    }
}
