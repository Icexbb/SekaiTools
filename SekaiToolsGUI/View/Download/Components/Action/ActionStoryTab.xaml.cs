using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SekaiDataFetch;
using SekaiDataFetch.Data;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;
using SekaiDataFetch.Source;
using SekaiToolsGUI.ViewModel;

namespace SekaiToolsGUI.View.Download.Components.Action;

public partial class ActionStoryTab : UserControl, IRefreshable
{
    private ActionStoryTabModel ViewModel => (ActionStoryTabModel)DataContext;
    private ListActionStory ActionStory { get; } = new();

    public ActionStoryTab()
    {
        DataContext ??= new ActionStoryTabModel();
        InitializeComponent();
    }

    private SourceType GetSourceType()
    {
        var parent = Parent;
        while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);

        return (parent as DownloadPage)?.GetSourceType() ?? throw new NullReferenceException();
    }


    public async Task Refresh()
    {
        CardUnits.IsEnabled = false;
        ActionStory.SetSource(GetSourceType());
        ActionStory.SetProxy(SettingPageModel.Instance.GetProxy());
        await ActionStory.Refresh();
        InitializeAreas();
        RefreshItems(true);
        CardUnits.IsEnabled = true;
    }


    private void ActionStoryTab_OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeAreas();
        RefreshItems(true);
    }

    public void InitializeAreas()
    {
        ViewModel.Areas = ActionStory?.Areas.ToArray() ?? [];
        BoxType.SelectedIndex = 0;
        RefreshItems(true);
    }

    private void CheckBoxUnit_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        switch (checkBox.Name)
        {
            case "CheckBoxUnitLn":
            {
                CheckBoxChara1.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara2.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara3.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara4.IsChecked = CheckBoxUnitLn.IsChecked;
                CheckBoxChara27.IsChecked = CheckBoxUnitLn.IsChecked;
            }
                break;
            case "CheckBoxUnitMmj":
            {
                CheckBoxChara5.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara6.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara7.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara8.IsChecked = CheckBoxUnitMmj.IsChecked;
                CheckBoxChara28.IsChecked = CheckBoxUnitMmj.IsChecked;
            }
                break;
            case "CheckBoxUnitVbs":
            {
                CheckBoxChara9.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara10.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara11.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara12.IsChecked = CheckBoxUnitVbs.IsChecked;
                CheckBoxChara29.IsChecked = CheckBoxUnitVbs.IsChecked;
            }
                break;
            case "CheckBoxUnitWs":
            {
                CheckBoxChara13.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara14.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara15.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara16.IsChecked = CheckBoxUnitWs.IsChecked;
                CheckBoxChara30.IsChecked = CheckBoxUnitWs.IsChecked;
            }
                break;
            case "CheckBoxUnit25":
            {
                CheckBoxChara17.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara18.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara19.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara20.IsChecked = CheckBoxUnit25.IsChecked;
                CheckBoxChara31.IsChecked = CheckBoxUnit25.IsChecked;
            }
                break;
            case "CheckBoxUnitPiapro":
            {
                CheckBoxChara21.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara22.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara23.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara24.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara25.IsChecked = CheckBoxUnitPiapro.IsChecked;
                CheckBoxChara26.IsChecked = CheckBoxUnitPiapro.IsChecked;
            }
                break;
        }

        RefreshItems();
    }


    private void CheckBoxChara_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;
        EnsureUnitBox();
        RefreshItems();
    }

    private void EnsureUnitBox()
    {
        CheckBoxUnitLn.IsChecked = CheckBoxChara1.IsChecked == true && CheckBoxChara2.IsChecked == true &&
                                   CheckBoxChara3.IsChecked == true && CheckBoxChara4.IsChecked == true &&
                                   CheckBoxChara27.IsChecked == true;
        CheckBoxUnitMmj.IsChecked = CheckBoxChara5.IsChecked == true && CheckBoxChara6.IsChecked == true &&
                                    CheckBoxChara7.IsChecked == true && CheckBoxChara8.IsChecked == true &&
                                    CheckBoxChara28.IsChecked == true;
        CheckBoxUnitVbs.IsChecked = CheckBoxChara9.IsChecked == true && CheckBoxChara10.IsChecked == true &&
                                    CheckBoxChara11.IsChecked == true && CheckBoxChara12.IsChecked == true &&
                                    CheckBoxChara29.IsChecked == true;
        CheckBoxUnitWs.IsChecked = CheckBoxChara13.IsChecked == true && CheckBoxChara14.IsChecked == true &&
                                   CheckBoxChara15.IsChecked == true && CheckBoxChara16.IsChecked == true &&
                                   CheckBoxChara30.IsChecked == true;
        CheckBoxUnit25.IsChecked = CheckBoxChara17.IsChecked == true && CheckBoxChara18.IsChecked == true &&
                                   CheckBoxChara19.IsChecked == true && CheckBoxChara20.IsChecked == true &&
                                   CheckBoxChara31.IsChecked == true;
        CheckBoxUnitPiapro.IsChecked = CheckBoxChara21.IsChecked == true && CheckBoxChara22.IsChecked == true &&
                                       CheckBoxChara23.IsChecked == true && CheckBoxChara24.IsChecked == true &&
                                       CheckBoxChara25.IsChecked == true && CheckBoxChara26.IsChecked == true;
    }

    private void ButtonSelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        ChangeAllBannerSelector(true);
    }

    private void ButtonUnSelectAll_OnClick(object sender, RoutedEventArgs e)
    {
        ChangeAllBannerSelector(false);
    }

    private void ChangeAllBannerSelector(bool to)
    {
        CheckBoxChara1.IsChecked = to;
        CheckBoxChara2.IsChecked = to;
        CheckBoxChara3.IsChecked = to;
        CheckBoxChara4.IsChecked = to;

        CheckBoxChara5.IsChecked = to;
        CheckBoxChara6.IsChecked = to;
        CheckBoxChara7.IsChecked = to;
        CheckBoxChara8.IsChecked = to;

        CheckBoxChara9.IsChecked = to;
        CheckBoxChara10.IsChecked = to;
        CheckBoxChara11.IsChecked = to;
        CheckBoxChara12.IsChecked = to;

        CheckBoxChara13.IsChecked = to;
        CheckBoxChara14.IsChecked = to;
        CheckBoxChara15.IsChecked = to;
        CheckBoxChara16.IsChecked = to;

        CheckBoxChara17.IsChecked = to;
        CheckBoxChara18.IsChecked = to;
        CheckBoxChara19.IsChecked = to;
        CheckBoxChara20.IsChecked = to;

        CheckBoxChara21.IsChecked = to;
        CheckBoxChara22.IsChecked = to;
        CheckBoxChara23.IsChecked = to;
        CheckBoxChara24.IsChecked = to;
        CheckBoxChara25.IsChecked = to;
        CheckBoxChara26.IsChecked = to;

        CheckBoxChara27.IsChecked = to;
        CheckBoxChara28.IsChecked = to;
        CheckBoxChara29.IsChecked = to;
        CheckBoxChara30.IsChecked = to;
        CheckBoxChara31.IsChecked = to;

        CheckBoxUnitLn.IsChecked = to;
        CheckBoxUnitMmj.IsChecked = to;
        CheckBoxUnitVbs.IsChecked = to;
        CheckBoxUnitWs.IsChecked = to;
        CheckBoxUnit25.IsChecked = to;
        CheckBoxUnitPiapro.IsChecked = to;

        RefreshItems();
    }

    private int _currentDirection = 1;

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
    private void RefreshItems(bool selectAll = false)
    {
        if (ActionStory == null || ActionStory.Data.Count == 0) return;

        var data = ActionStory.Data.Select(item => (AreaStory)item.Clone()).ToList();
        data.Sort((x, y) => _currentDirection * x.ActionSet.Id.CompareTo(y.ActionSet.Id));
        if (selectAll) ChangeAllBannerSelector(true);
        ViewModel.EventStories = data.Where(JudgeVisibility).ToArray();
    }

    private bool JudgeVisibility(AreaStory data)
    {
        var visibility = ((Area)BoxType.SelectedItem).Id == data.ActionSet.AreaId;
        if (!visibility) return visibility;
        if (ViewModel.AllChecked || ViewModel.AllUnchecked) return visibility;

        List<int> selectedIds = [];

        if (ViewModel.CheckBoxChara01) selectedIds.Add(01);
        if (ViewModel.CheckBoxChara02) selectedIds.Add(02);
        if (ViewModel.CheckBoxChara03) selectedIds.Add(03);
        if (ViewModel.CheckBoxChara04) selectedIds.Add(04);
        if (ViewModel.CheckBoxChara05) selectedIds.Add(05);
        if (ViewModel.CheckBoxChara06) selectedIds.Add(06);
        if (ViewModel.CheckBoxChara07) selectedIds.Add(07);
        if (ViewModel.CheckBoxChara08) selectedIds.Add(08);
        if (ViewModel.CheckBoxChara09) selectedIds.Add(09);
        if (ViewModel.CheckBoxChara10) selectedIds.Add(10);
        if (ViewModel.CheckBoxChara11) selectedIds.Add(11);
        if (ViewModel.CheckBoxChara12) selectedIds.Add(12);
        if (ViewModel.CheckBoxChara13) selectedIds.Add(13);
        if (ViewModel.CheckBoxChara14) selectedIds.Add(14);
        if (ViewModel.CheckBoxChara15) selectedIds.Add(15);
        if (ViewModel.CheckBoxChara16) selectedIds.Add(16);
        if (ViewModel.CheckBoxChara17) selectedIds.Add(17);
        if (ViewModel.CheckBoxChara18) selectedIds.Add(18);
        if (ViewModel.CheckBoxChara19) selectedIds.Add(19);
        if (ViewModel.CheckBoxChara20) selectedIds.Add(20);
        if (ViewModel.CheckBoxChara21) selectedIds.Add(21);
        if (ViewModel.CheckBoxChara22) selectedIds.Add(22);
        if (ViewModel.CheckBoxChara23) selectedIds.Add(23);
        if (ViewModel.CheckBoxChara24) selectedIds.Add(24);
        if (ViewModel.CheckBoxChara25) selectedIds.Add(25);
        if (ViewModel.CheckBoxChara26) selectedIds.Add(26);
        if (ViewModel.CheckBoxChara27) selectedIds.Add(27);
        if (ViewModel.CheckBoxChara28) selectedIds.Add(28);
        if (ViewModel.CheckBoxChara29) selectedIds.Add(29);
        if (ViewModel.CheckBoxChara30) selectedIds.Add(30);
        if (ViewModel.CheckBoxChara31) selectedIds.Add(31);

        return data.ActionSet.CharacterIds.All(i => selectedIds.Contains(i));
    }
}