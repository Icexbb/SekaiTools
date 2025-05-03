using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SekaiDataFetch.Item;
using SekaiDataFetch.List;

namespace SekaiToolsGUI.View.Download.Components.Action;

public partial class ActionStoryItem : UserControl
{
    public static readonly DependencyProperty AreaStoryProperty = DependencyProperty.Register(
        nameof(AreaStory), typeof(AreaStory), typeof(ActionStoryItem),
        new PropertyMetadata(null, OnAreaStoryChanged));

    public AreaStory AreaStory { get; set; } = null!;

    private static void OnAreaStoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ActionStoryItem control && e.NewValue is AreaStory areaStory) control.Initialize(areaStory);
    }


    public ActionStoryItem()
    {
        InitializeComponent();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DependencyObject? parent = this;
            while (parent != null && parent is not DownloadPage) parent = VisualTreeHelper.GetParent(parent);
            (parent as DownloadPage)?.AddTask(AreaStory.ActionSet.ScriptId, AreaStory.Url());
        });
    }


    private void Initialize(AreaStory areaStory)
    {
        AreaStory = areaStory;
        Icons.Children.Clear();
        var cIds = areaStory.CharacterIds.ToList();
        cIds.Sort();
        foreach (var id in cIds)
        {
            if (id is < 1 or > 31)
            {
                var text = new TextBlock
                {
                    Text = $"L2d-{id}",
                    FontSize = 16,
                    Margin = new Thickness(5, 0, 5, 0)
                };
                Icons.Children.Add(text);
            }
            else
            {
                var url = $"pack://application:,,,/Resource/Characters/chr_{id}.png";
                var icon = new Image
                {
                    Source = new BitmapImage(new Uri(url)),
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(5, 0, 5, 0)
                };
                Icons.Children.Add(icon);
            }
        }

        KeyText.Text = $"{areaStory.ActionSet.Id} {areaStory.ActionSet.ScenarioId}";
    }
}