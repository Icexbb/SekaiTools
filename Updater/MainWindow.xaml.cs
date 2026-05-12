using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;

namespace Updater;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }


    private string MainAppName => "SekaiToolsGUI";
    private string MainAppPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{MainAppName}.exe");


    private string GetLocalVersion()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{MainAppName}.exe");
        if (!File.Exists(path)) return "0.0.0";
        var vi = FileVersionInfo.GetVersionInfo(path);
        return vi.FileVersion ?? "0.0.0";
    }

    private async Task<string> GetLatestVersionAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Updater", "1.0"));
        var json = await client.GetStringAsync("https://api.github.com/repos/Icexbb/SekaiTools/releases/latest");

        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.GetProperty("tag_name").GetString();
        return version?.TrimStart('v') ?? "0.0.0";
    }

    private async Task DownloadFileAsync(string url, string destFile, string version = "")
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
                StatusText.Text = $"新版本 {version}\n" +
                                  $"正在下载更新包... {Progress.Value:F0}%";
            });
        }
    }

    private void Extract7Z(string filePath, string targetDir)
    {
        // 确保先关闭主程序
        foreach (var p in Process.GetProcessesByName(MainAppName)) p.Kill();


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

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch (IOException)
            {
                // 被当前进程锁定的文件（Updater.dll 等）稍后由批处理脚本替换
            }
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            var dirName = Path.GetFileName(dir);
            CopyDirectoryRecursive(dir, Path.Combine(dest, dirName));
        }
    }

    private void StartMainApp(string tempDir)
    {
        // 为被锁定的 Updater 文件创建一个后续替换脚本
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "post_update.cmd");
        var pid = Environment.ProcessId;
        var script =
            $"@echo off\r\n" +
            $":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" | findstr \"{pid}\" > nul\r\n" +
            $"if not errorlevel 1 (\r\n" +
            $"    timeout /t 1 /nobreak > nul\r\n" +
            $"    goto wait\r\n" +
 $")\r\n" +
            $"xcopy /y /e /q \"{tempDir}\\SekaiTools\\*\" \"{AppDomain.CurrentDomain.BaseDirectory}\" > nul\r\n" +
            $"rmdir /s /q \"{tempDir}\"\r\n" +
            $"start \"\" \"{MainAppPath}\"\r\n" +
            $"del \"{scriptPath}\"\r\n";
        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        Application.Current.Shutdown();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.TextAlignment = TextAlignment.Center;
            StatusText.Text = "正在检查版本...";

            var remoteVersion = await GetLatestVersionAsync();
            var localVersion = GetLocalVersion();

            if (Version.TryParse(localVersion, out var local) &&
                Version.TryParse(remoteVersion, out var remote) &&
                local >= remote)
            {
                StatusText.Text = "已是最新版本";
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            var url = $"https://github.com/Icexbb/SekaiTools/releases/download/" +
                      $"{remoteVersion}/SekaiTools-{remoteVersion}.7z";
            var zipFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update.7z");
            if (File.Exists(zipFile)) File.Delete(zipFile);

            await DownloadFileAsync(url, zipFile, remoteVersion);

            StatusText.Text = "正在解压更新包...";
            var targetDir = AppDomain.CurrentDomain.BaseDirectory;
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");

            Extract7Z(zipFile, tempDir);
            File.Delete(zipFile);
            StatusText.Text = "正在更新文件...";

            var sourceDir = Path.Combine(tempDir, "SekaiTools");
            CopyDirectoryRecursive(sourceDir, targetDir);

            StatusText.Text = "更新完成，正在启动主程序...";
            await Task.Delay(1000);
            StartMainApp(tempDir);
        }
        catch (Exception ex)
        {
            StatusText.TextAlignment = TextAlignment.Left;
            StatusText.Text = "更新失败：" + "\n" + ex.Message + "\n" + ex.StackTrace;
            Progress.Visibility = Visibility.Collapsed;
        }
    }
}