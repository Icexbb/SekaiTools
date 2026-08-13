using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.Interface;
using SekaiToolsGUI.ViewModel.Download;
using SekaiToolsGUI.ViewModel.Setting;

namespace SekaiToolsGUI.View.Download.Components.Event;

public partial class EventStoryTab : UserControl, IRefreshable
{
    private static readonly IReadOnlyDictionary<int, string> CharacterNames = new Dictionary<int, string>
    {
        [1] = "星乃一歌", [2] = "天马咲希", [3] = "望月穗波", [4] = "日野森志步",
        [5] = "花里实乃理", [6] = "桐谷遥", [7] = "桃井爱莉", [8] = "日野森雫",
        [9] = "小豆泽心羽", [10] = "白石杏", [11] = "东云彰人", [12] = "青柳冬弥",
        [13] = "天马司", [14] = "凤笑梦", [15] = "草薙宁宁", [16] = "神代类",
        [17] = "宵崎奏", [18] = "朝比奈真冬", [19] = "东云绘名", [20] = "晓山瑞希",
        [21] = "初音未来", [22] = "镜音铃", [23] = "镜音连", [24] = "巡音流歌",
        [25] = "MEIKO", [26] = "KAITO",
        [27] = "初音未来", [28] = "初音未来", [29] = "初音未来",
        [30] = "初音未来", [31] = "初音未来"
    };

    private static readonly (string GroupName, int[] CharacterIds)[] CharacterGroups =
    [
        ("全部", [0]),
        ("Leo/need", [1, 2, 3, 4, 27]),
        ("MORE MORE JUMP！", [5, 6, 7, 8, 28]),
        ("Vivid BAD SQUAD", [9, 10, 11, 12, 29]),
        ("Wonderlands×Showtime", [13, 14, 15, 16, 30]),
        ("25时，在Nightcord。", [17, 18, 19, 20, 31]),
        ("Piapro Characters", [21, 22, 23, 24, 25, 26])
    ];

    private int _currentDirection = -1;

    public EventStoryTab()
    {
        DataContext ??= new EventStoryTabModel();
        InitializeComponent();
        InitializeCharacterComboBox();
    }

    private EventStoryTabModel ViewModel => (EventStoryTabModel)DataContext;
    private ListEventStory ListEventStory => ListEventStory.Instance;

    public async Task Refresh()
    {
        CardCharacters.IsEnabled = false;
        CardUnits.IsEnabled = false;
        try
        {
            ListEventStory.SetSource(GetSourceType());
            ListEventStory.SetProxy(SettingPageModel.Instance.GetProxy());
            await ListEventStory.Refresh();
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

    private void InitializeCharacterComboBox()
    {
        var characters = CharacterGroups
            .SelectMany(group => group.CharacterIds.Select(characterId => CreateCharacterItem(group.GroupName,
                characterId)))
            .ToArray();

        ViewModel.Characters = characters;
        var groupedView = CollectionViewSource.GetDefaultView(characters);
        groupedView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CharacterComboBoxItem.GroupName)));
        CharacterComboBox.ItemsSource = groupedView;
        CharacterComboBox.SelectedIndex = 0;
    }

    private static CharacterComboBoxItem CreateCharacterItem(string groupName, int characterId)
    {
        return new CharacterComboBoxItem
        {
            GroupName = groupName,
            Name = characterId == 0 ? "全部角色" : CharacterNames[characterId],
            Value = characterId,
            GameCharacterId = characterId is >= 27 and <= 31 ? 21 : characterId,
            Source = characterId == 0
                ? "pack://application:,,,/Resource/icon.png"
                : $"pack://application:,,,/Resource/Characters/chr_{characterId}.png"
        };
    }

    private void CharacterComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void Filter_OnSelected(object sender, SelectionChangedEventArgs e)
    {
        RefreshItems();
    }

    private void EventStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        BoxType.SelectedIndex = 0;
        if (CharacterComboBox.SelectedIndex < 0) CharacterComboBox.SelectedIndex = 0;
        RefreshItems();
    }

    private void ButtonSort_OnClick(object sender, RoutedEventArgs e)
    {
        ButtonSort.RenderTransform = new ScaleTransform(-1, _currentDirection);
        _currentDirection *= -1;
        RefreshItems();
    }

    private void RefreshItems()
    {
        if (ListEventStory.Data.Count == 0) return;

        var data = ListEventStory.Data.Select(item => (EventStorySet)item.Clone()).ToList();
        data.Sort((x, y) => _currentDirection * x.EventStory.EventId.CompareTo(y.EventStory.EventId));
        ViewModel.EventStories = data.Where(JudgeVisibility).ToArray();
    }

    private bool JudgeVisibility(EventStorySet data)
    {
        string[] filterTypes = BoxType.SelectedIndex switch
        {
            0 => ["marathon", "cheerful_carnival", "world_bloom"],
            1 => ["marathon"],
            2 => ["cheerful_carnival"],
            3 => ["world_bloom"],
            _ => throw new ArgumentOutOfRangeException()
        };

        if (!filterTypes.Contains(data.GameEvent.EventType)) return false;

        if (CharacterComboBox.SelectedItem is not CharacterComboBoxItem character || character.Value == 0)
            return true;

        return data.GameEvent.EventType == "world_bloom"
            ? data.BannerGameCharacterIds.Contains(character.GameCharacterId)
            : data.EventStory.BannerGameCharacterUnitId == character.Value;
    }
}
