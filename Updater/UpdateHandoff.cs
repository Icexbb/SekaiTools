using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Updater;

internal static class UpdateHandoff
{
    internal static void StartInstaller(string install, string stage)
    {
        var bundled = Path.Combine(install, "tools", "updater", "Updater.exe");
        if (!File.Exists(bundled)) bundled = Path.Combine(install, "Updater.exe");
        var work = UpdatePaths.WorkDirectory(install);
        Directory.CreateDirectory(work);
        var helper = Path.Combine(work, "Updater.exe");
        using (UpdatePaths.AcquireLock(work))
            File.Copy(bundled, helper, overwrite: true);

        using var parent = Process.GetCurrentProcess();
        using var child = UpdatePaths.Start(helper, work, "--install", install, stage,
            parent.Id.ToString(CultureInfo.InvariantCulture),
            parent.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
    }
}
