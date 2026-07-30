using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using SekaiToolsAvalonia.Interface;
using SekaiToolsAvalonia.Services;
using SekaiToolsAvalonia.ViewModel;

namespace SekaiToolsAvalonia;

public partial class MainWindow : Window
{
    private readonly Dictionary<Type, Control> _pageCache = new();
    private SnackbarService? _snackbar;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    public static SnackbarService? Snackbar =>
        (Application.Current?.ApplicationLifetime as
            IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow is MainWindow mw
            ? mw._snackbar
            : null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        Opened += OnMainWindowOpened;
    }

    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnMainWindowOpened;
        _snackbar = new SnackbarService(SnackbarHost);

        NavListBox.SelectionChanged += OnNavSelectionChanged;
        FooterListBox.SelectionChanged += OnFooterSelectionChanged;

        if (NavListBox.ItemCount > 0) NavListBox.SelectedIndex = 0;
    }

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is NavItem navItem)
        {
            FooterListBox.SelectedIndex = -1;
            NavigateTo(navItem);
        }
    }

    private void OnFooterSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is NavItem navItem)
        {
            NavListBox.SelectedIndex = -1;
            NavigateTo(navItem);
        }
    }

    private void NavigateTo(NavItem navItem)
    {
        var pageType = navItem.TargetPageType;

        // Check cache first
        if (navItem.CachePage && _pageCache.TryGetValue(pageType, out var cachedPage))
        {
            PageContent.Content = cachedPage;
            if (cachedPage is IAppPage appPage)
                appPage.OnNavigatedTo();
            return;
        }

        // Create new page instance
        if (Activator.CreateInstance(pageType) is Control page)
        {
            if (navItem.CachePage)
                _pageCache[pageType] = page;

            PageContent.Content = page;
            if (page is IAppPage appPage)
                appPage.OnNavigatedTo();
        }
    }
}