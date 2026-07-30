using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace SekaiToolsAvalonia.Services;

public class SnackbarService
{
    private readonly TimeSpan _defaultDuration = TimeSpan.FromSeconds(3);
    private readonly Panel _host;

    public SnackbarService(Panel host)
    {
        _host = host;
    }

    public void Show(string message, TimeSpan? duration = null)
    {
        var snackbar = new Border
        {
            Background = Brushes.Black,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10),
            MaxWidth = 400,
            Opacity = 0,
            Child = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
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