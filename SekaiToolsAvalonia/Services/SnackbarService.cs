using Avalonia.Controls;
using Avalonia.Threading;

namespace SekaiToolsAvalonia.Services;

public class SnackbarService
{
    private readonly Panel _host;
    private readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(3);

    public SnackbarService(Panel host)
    {
        _host = host;
    }

    public void Show(string message, TimeSpan? duration = null)
    {
        var snackbar = new Border
        {
            Background = Avalonia.Media.Brushes.Black,
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(16, 10),
            MaxWidth = 400,
            Opacity = 0,
            Child = new TextBlock
            {
                Text = message,
                Foreground = Avalonia.Media.Brushes.White,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = 13
            }
        };

        _host.Children.Add(snackbar);

        Dispatcher.UIThread.Post(async () =>
        {
            for (double i = 0; i <= 1; i += 0.1)
            {
                snackbar.Opacity = i;
                await Task.Delay(20);
            }

            await Task.Delay(duration ?? _defaultDuration);

            for (double i = 1; i >= 0; i -= 0.1)
            {
                snackbar.Opacity = i;
                await Task.Delay(20);
            }

            _host.Children.Remove(snackbar);
        });
    }
}
