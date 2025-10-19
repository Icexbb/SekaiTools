using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace Updater;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }


    string MainAppName => "SekaiToolsGUI";
    string MainAppPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{MainAppName}.exe");


    private async Task<string> GetLatestVersionAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Updater", "1.0"));
        var json = await client.GetStringAsync("https://api.github.com/repos/Icexbb/SekaiTools/releases/latest");

        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.GetProperty("tag_name").GetString();
        return version?.TrimStart('v') ?? "0.0.0";
    }

    private async Task DownloadFileAsync(string url, string destFile,string version = "")
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(destFile);

        var buffer = new byte[8192];
        long read = 0;
        int bytes;
        while ((bytes = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, bytes));
            read += bytes;
            var read1 = read;
            Dispatcher.Invoke(() =>
            {
                Progress.Value = (double)read1 / total * 100;
                StatusText.Text = $"新版本{version}\n" +
                                  $"正在下载更新包... {Progress.Value:F0}%";
            });
        }
    }

    private void Extract7Z(string filePath, string targetDir)
    {
        // 确保先关闭主程序
        foreach (var p in Process.GetProcessesByName(MainAppName))
        {
            p.Kill();
        }

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zr.exe"),
            Arguments = $"x \"{filePath}\" -y -o\"{targetDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        var p2 = Process.Start(psi);
        p2?.WaitForExit();
    }

    private void StartMainApp()
    {
        if (File.Exists(MainAppPath))
        {
            Process.Start(new ProcessStartInfo(MainAppPath) { UseShellExecute = true });
        }

        Application.Current.Shutdown();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "正在检查版本...";

            var remoteVersion = await GetLatestVersionAsync();
            var url = $"https://github.com/Icexbb/SekaiTools/releases/download/" +
                      $"{remoteVersion}/SekaiTools-{remoteVersion}.7z";
            var zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.7z");
            if (File.Exists(zipFile)) File.Delete(zipFile);

            await DownloadFileAsync(url, zipFile);

            StatusText.Text = "正在解压更新包...";
            Extract7Z(zipFile, AppDomain.CurrentDomain.BaseDirectory);
            File.Delete(zipFile);

            StatusText.Text = "更新完成，正在启动主程序...";
            await Task.Delay(1000);
            StartMainApp();
        }
        catch (Exception ex)
        {
            StatusText.Text = "更新失败：" + ex.Message;
        }
    }
}