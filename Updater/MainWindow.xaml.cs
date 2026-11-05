using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;

namespace Updater;

public partial class MainWindow : Window
{
    private string? _errorText;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += PrepareUpdate;
        Closed += (_, _) => Application.Current.Shutdown();
    }

    private async void PrepareUpdate(object sender, RoutedEventArgs e)
    {
        Loaded -= PrepareUpdate;
        var install = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        if (!File.Exists(Path.Combine(install, UpdatePaths.MainExecutable)))
            install = Path.GetFullPath(Path.Combine(install, "..", ".."));
        var stage = Path.Combine(UpdatePaths.WorkDirectory(install), "stage-" + Guid.NewGuid().ToString("N"));
        var handedOff = false;
        try
        {
            var work = UpdatePaths.WorkDirectory(install);
            if (File.Exists(Path.Combine(work, UpdatePaths.Journal)))
            {
                using var parent = Process.GetCurrentProcess();
                using var recovery = UpdatePaths.Start(Path.Combine(work, "Updater.exe"), work, "--recover", install,
                    parent.Id.ToString(CultureInfo.InvariantCulture),
                    parent.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
                Application.Current.Shutdown();
                return;
            }

            var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "SekaiTools", "Data", "setting.json");
            var proxy = UpdateProxySettings.Load(
                Environment.GetEnvironmentVariable(UpdateProxySettings.EnvironmentVariable), settingsPath);
            var progress = new Progress<double>(value =>
            {
                Progress.IsIndeterminate = false;
                Progress.Value = value * 100;
                StatusText.Text = $"正在下载更新包… {value:P0}";
            });
            var version = await UpdatePackage.PrepareAsync(install, stage, proxy.CreateHandler(), progress: progress);
            if (version is null)
            {
                StatusText.Text = "已是最新版本";
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            UpdateHandoff.StartInstaller(install, stage);
            handedOff = true;
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _errorText = $"更新失败：{ex.Message}";
            StatusText.Text = _errorText;
            Progress.Visibility = Visibility.Collapsed;
            CopyButton.Visibility = Visibility.Visible;
        }
        finally
        {
            if (!handedOff)
            {
                try { UpdateInstallation.DeleteDirectory(stage); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_errorText ?? StatusText.Text);
        CopyButton.Content = "已复制";
    }
}
