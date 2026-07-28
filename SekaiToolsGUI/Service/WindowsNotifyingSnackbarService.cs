using Wpf.Ui;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.Service;

public sealed class WindowsNotifyingSnackbarService : ISnackbarService
{
    private readonly SnackbarService _snackbarService = new();

    public TimeSpan DefaultTimeOut
    {
        get => _snackbarService.DefaultTimeOut;
        set => _snackbarService.DefaultTimeOut = value;
    }

    public void SetSnackbarPresenter(SnackbarPresenter contentPresenter)
    {
        _snackbarService.SetSnackbarPresenter(contentPresenter);
    }

    public SnackbarPresenter GetSnackbarPresenter()
    {
        return _snackbarService.GetSnackbarPresenter()!;
    }

    public void Show(string title, string message, ControlAppearance appearance,
        IconElement? icon, TimeSpan timeout)
    {
        _snackbarService.Show(title, message, appearance, icon, timeout);
        WindowsNotificationService.Show(title, message);
    }
}
