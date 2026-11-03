using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Logging;
using SekaiToolsBase;
using SekaiToolsGUI.ViewModel.Setting;
using Updater;

namespace SekaiToolsGUI;

public partial class MainWindow
{
    private readonly CancellationTokenSource _updateCancellation = new();
    private Task _updatePreparation = Task.CompletedTask;
    private string? _updateStage;
    private bool _restartForUpdate;

    private void OnUpdateContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnUpdateContentRendered;
        var ready = Environment.GetEnvironmentVariable(UpdatePaths.ReadyEvent);
        var skip = Environment.GetEnvironmentVariable(UpdatePaths.SkipUpdate);
        Environment.SetEnvironmentVariable(UpdatePaths.ReadyEvent, null);
        Environment.SetEnvironmentVariable(UpdatePaths.SkipUpdate, null);
        if (ready is not null && EventWaitHandle.TryOpenExisting(ready, out var signal))
        {
            using (signal) signal.Set();
        }

        if (_useCheckUpdate && skip is null)
            _updatePreparation = PrepareUpdateAsync(_updateCancellation.Token);
    }

    private async Task PrepareUpdateAsync(CancellationToken cancellationToken)
    {
        var install = AppContext.BaseDirectory;
        if (!File.Exists(Path.Combine(install, "tools", "updater", "Updater.exe"))) return;
        var stage = Path.Combine(UpdatePaths.WorkDirectory(install), "stage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var proxy = SettingPageModel.Instance.GetProxy();
            var handler = new HttpClientHandler
            {
                Proxy = proxy.ProxyType switch
                {
                    Proxy.Type.Http => new WebProxy(new UriBuilder("http", proxy.Host, proxy.Port).Uri),
                    Proxy.Type.Socks5 => new WebProxy(new UriBuilder("socks5", proxy.Host, proxy.Port).Uri),
                    _ => null
                }
            };

            var progress = new Progress<double>(value =>
            {
                UpdateButton.Content = $"正在下载更新… {value:P0}";
                UpdateButton.Visibility = Visibility.Visible;
            });
            var version = await UpdatePackage.PrepareAsync(install, stage, handler, cancellationToken, progress);
            cancellationToken.ThrowIfCancellationRequested();
            if (version is null) return;
            _updateStage = stage;
            UpdateButton.Content = $"重启并更新到 {version}";
            UpdateButton.IsEnabled = true;
            UpdateButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Log($"准备更新失败：{ex}", LogLevel.Warning);
            UpdateButton.Visibility = Visibility.Collapsed;
        }
        finally
        {
            if (_updateStage is null) DeleteUpdateStage(stage);
        }
    }

    private void RestartForUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_closeInProgress || _updateStage is null) return;
        _restartForUpdate = true;
        Close();
    }

    private async Task FinishUpdateOnCloseAsync()
    {
        _updateCancellation.Cancel();
        await _updatePreparation;
        if (_updateStage is not { } stage) return;
        if (_restartForUpdate)
        {
            UpdateHandoff.StartInstaller(AppContext.BaseDirectory, stage);
        }
        else
        {
            DeleteUpdateStage(stage);
        }

        _updateStage = null;
    }

    private static void DeleteUpdateStage(string stage)
    {
        try
        {
            if (Directory.Exists(stage)) Directory.Delete(stage, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Log($"清理更新暂存目录失败：{ex.Message}", LogLevel.Warning);
        }
    }

    private static string GetLocalVersion()
    {
        var path = Assembly.GetEntryAssembly()?.Location;
        return path is not null && File.Exists(path)
            ? FileVersionInfo.GetVersionInfo(path).FileVersion ?? "0.0.0"
            : "0.0.0";
    }
}
