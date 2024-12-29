using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.List;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download.Components.Card;

public partial class CardStoryTab : UserControl, IRefreshable
{
    private CardStoryTabModel ViewModel => (CardStoryTabModel)DataContext;
    private ListCardStory? CardStory { get; set; }

    public CardStoryTab()
    {
        DataContext ??= new CardStoryTabModel();
        InitializeComponent();
        CharacterComboBox_Init();
    }

    private SourceType GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }

    public async Task Refresh()
    {
        CardStory ??= new ListCardStory(GetSourceType(), SettingPageModel.Instance.GetProxy());
        await CardStory.Refresh();
        CharacterComboBox_OnSelectionChanged(null!, null!);
    }

    private void CharacterComboBox_Init()
    {
        var characterList = new List<CharacterComboBoxItem>();
        foreach (var (key, value) in Constants.CharacterIdToName)
        {
            if (key > 26) break;
            characterList.Add(new CharacterComboBoxItem
            {
                Name = value,
                Value = key,
                Source = $"pack://application:,,,/Resource/Characters/chr_{key}.png"
            });
        }

        ViewModel.Characters = characterList.ToArray();
        CharacterComboBox.SelectedIndex = 0;
    }

    private void CharacterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CharacterComboBox.SelectedItem is not CharacterComboBoxItem item) return;
        ViewModel.CardStories = CardStory?.Data.Where(x => x.Card.CharacterId == item.Value).ToArray() ?? [];
    }

    private void CardStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        CardStory ??= new ListCardStory(GetSourceType(), SettingPageModel.Instance.GetProxy());
        CharacterComboBox.SelectedIndex = 0;
        CharacterComboBox_OnSelectionChanged(null!, null!);
    }
}