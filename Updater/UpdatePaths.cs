using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Updater;

internal static class UpdatePaths
{
    internal const string MainExecutable = "SekaiToolsGUI.exe";
    internal const string Journal = "install.json";
    internal const string Manifest = "update-files.txt";
    internal const string ReadyEvent = "SEKAI_TOOLS_UPDATE_READY";
    internal const string SkipUpdate = "SEKAI_TOOLS_SKIP_UPDATE";

    internal static string WorkDirectory(string installDirectory)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installDirectory));
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant())));
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SekaiTools", "Update", key);
    }

    internal static Process Start(string executable, string workingDirectory, params string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException($"无法启动 {executable}");
    }

    internal static FileStream AcquireLock(string work) =>
        new(Path.Combine(work, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
