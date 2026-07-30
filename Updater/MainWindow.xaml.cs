using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace Updater;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string? _errorText;

    private ProxyConfig? _proxyConfig;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }


    private string MainAppName => "SekaiToolsGUI";
    private string MainAppPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{MainAppName}.exe");

    private static string SettingFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Data", "setting.json");

    private ProxyConfig GetProxyConfig()
    {
        if (_proxyConfig != null) return _proxyConfig;
        try
        {
            _proxyConfig = LoadProxySettings();
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }

        return _proxyConfig ??= new ProxyConfig(0, "127.0.0.1", 1080);
    }

    private static ProxyConfig LoadProxySettings()
    {
        var json = File.ReadAllText(SettingFilePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new ProxyConfig(
            root.TryGetProperty("ProxyType", out var t) ? t.GetInt32() : 0,
            root.TryGetProperty("ProxyHost", out var h) ? h.GetString() ?? "127.0.0.1" : "127.0.0.1",
            root.TryGetProperty("ProxyPort", out var p) ? p.GetInt32() : 1080
        );
    }

    private HttpClient CreateHttpClient()
    {
        var config = GetProxyConfig();
        HttpMessageHandler handler = config.Type switch
        {
            0 => new HttpClientHandler(), // None
            1 => new HttpClientHandler // HTTP
            {
                Proxy = new WebProxy(new Uri($"http://{config.Host}:{config.Port}")),
                UseProxy = true
            },
            2 => new SocketsHttpHandler // Socks5 → HTTP CONNECT (WebProxy 不支持真正的 SOCKS5)
            {
                Proxy = new WebProxy(new Uri($"http://{config.Host}:{config.Port}")),
                UseProxy = true
            },
            _ => new HttpClientHandler()
        };
        return new HttpClient(handler);
    }


    private string GetLocalVersion()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{MainAppName}.exe");
        if (!File.Exists(path)) return "0.0.0";
        var vi = FileVersionInfo.GetVersionInfo(path);
        return vi.FileVersion ?? "0.0.0";
    }

    private async Task<string> GetLatestVersionAsync()
    {
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Updater", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(30);
        var json = await client.GetStringAsync("https://api.github.com/repos/Icexbb/SekaiTools/releases/latest");

        using var doc = JsonDocument.Parse(json);
        var version = doc.RootElement.GetProperty("tag_name").GetString();
        return version?.TrimStart('v')?.Split('-')[0] ?? "0.0.0";
    }

    private async Task DownloadFileAsync(string url, string destFile, string version = "")
    {
        using var client = CreateHttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var file = File.Create(destFile);

        var buffer = new byte[8192];
        long read = 0;
        int bytes;
        while ((bytes = await ReadChunkAsync(stream, buffer, TimeSpan.FromSeconds(30))) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, bytes));
            read += bytes;
            var read1 = read;
            Dispatcher.Invoke(() =>
            {
                Progress.IsIndeterminate = total <= 0;
                if (total > 0)
                    Progress.Value = (double)read1 / total * 100;
                StatusText.Text = $"新版本 {version}\n" +
                                  (total > 0
                                      ? $"正在下载更新包... {Progress.Value:F0}%"
                                      : $"正在下载更新包... {read1 / 1024d / 1024d:F1} MB");
            });
        }
    }

    private async Task VerifyPackageAsync(string packagePath, string checksumUrl)
    {
        using var client = CreateHttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var checksumText = await client.GetStringAsync(checksumUrl);
        var expected = checksumText.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected) || expected.Length != 64)
            throw new InvalidDataException("更新包校验文件格式无效");

        await using var stream = File.OpenRead(packagePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("更新包 SHA-256 校验失败，已停止更新");
    }

    private static async Task<int> ReadChunkAsync(Stream stream, byte[] buffer, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            return await stream.ReadAsync(buffer, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("下载卡死：超过 30 秒未收到数据");
        }
    }

    private void Extract7Z(string filePath, string targetDir)
    {
        var extractorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zr.exe");
        if (!File.Exists(extractorPath))
            throw new FileNotFoundException("更新解压程序不存在", extractorPath);
        var psi = new ProcessStartInfo
        {
            FileName = extractorPath,
            Arguments = $"x \"{filePath}\" -y -o\"{targetDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };
        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("无法启动更新解压程序");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidDataException($"更新包解压失败，7zr 退出码: {process.ExitCode}");
    }

    private void StartMainApp(string tempDir)
    {
        // Updater 退出后再替换全部文件；复制失败时用备份恢复旧版本。
        var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "post_update.cmd");
        var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sourceDir = Path.Combine(tempDir, "SekaiTools");
        var backupDir = Path.Combine(tempDir, "backup");
        var failureLog = Path.Combine(installDir, "update_failed.txt");
        var pid = Environment.ProcessId;
        var script =
            $"@echo off\r\n" +
            $":wait\r\n" +
            $"tasklist /FI \"PID eq {pid}\" | findstr \"{pid}\" > nul\r\n" +
            $"if not errorlevel 1 (\r\n" +
            $"    timeout /t 1 /nobreak > nul\r\n" +
            $"    goto wait\r\n" +
            $")\r\n" +
            $"taskkill /IM \"{MainAppName}.exe\" /F > nul 2>&1\r\n" +
            $"xcopy /y /e /i /h /q \"{installDir}\\*\" \"{backupDir}\\\" > nul\r\n" +
            $"if errorlevel 1 goto backup_failed\r\n" +
            $"xcopy /y /e /i /h /q \"{sourceDir}\\*\" \"{installDir}\\\" > nul\r\n" +
            $"if errorlevel 1 goto rollback\r\n" +
            $"start \"\" \"{MainAppPath}\"\r\n" +
            $"rmdir /s /q \"{tempDir}\"\r\n" +
            $"del \"{scriptPath}\"\r\n";
        script +=
            $"exit /b 0\r\n" +
            $":rollback\r\n" +
            $"xcopy /y /e /i /h /q \"{backupDir}\\*\" \"{installDir}\\\" > nul\r\n" +
            $"echo 更新文件复制失败，已尝试恢复旧版本。> \"{failureLog}\"\r\n" +
            $"start \"\" \"{MainAppPath}\"\r\n" +
            $"del \"{scriptPath}\"\r\n" +
            $"exit /b 1\r\n" +
            $":backup_failed\r\n" +
            $"echo 无法备份当前版本，未执行更新。> \"{failureLog}\"\r\n" +
            $"start \"\" \"{MainAppPath}\"\r\n" +
            $"del \"{scriptPath}\"\r\n" +
            $"exit /b 1\r\n";
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
            var checksumUrl = url + ".sha256";
            var tempDir = Path.Combine(Path.GetTempPath(), $"SekaiToolsUpdate-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var zipFile = Path.Combine(tempDir, "update.7z");

            await DownloadFileAsync(url, zipFile, remoteVersion);
            StatusText.Text = "正在校验更新包...";
            Progress.IsIndeterminate = true;
            await VerifyPackageAsync(zipFile, checksumUrl);

            StatusText.Text = "正在解压更新包...";
            Extract7Z(zipFile, tempDir);
            File.Delete(zipFile);

            var sourceDir = Path.Combine(tempDir, "SekaiTools");
            if (!File.Exists(Path.Combine(sourceDir, $"{MainAppName}.exe")))
                throw new InvalidDataException("更新包目录结构无效，未找到主程序");

            StatusText.Text = "更新包已准备完成，正在安全替换文件...";
            await Task.Delay(1000);
            StartMainApp(tempDir);
        }
        catch (Exception ex)
        {
            StatusText.TextAlignment = TextAlignment.Left;
            _errorText = ex is TaskCanceledException
                ? "更新失败：检查更新超时，请检查网络连接"
                : "更新失败：" + ex.Message;
            StatusText.Text = _errorText;
            Progress.Visibility = Visibility.Collapsed;
            CopyButton.Visibility = Visibility.Visible;
            MaxHeight = SystemParameters.WorkArea.Height * 0.8;
            MaxWidth = SystemParameters.WorkArea.Width * 0.8;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_errorText ?? StatusText.Text);
            CopyButton.Content = "已复制";
        }
        catch
        {
            CopyButton.Content = "复制失败";
        }
    }

    private sealed record ProxyConfig(int Type, string Host, int Port);
}