using System.Globalization;
using System.IO;
using System.Windows;

namespace Updater;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 0)
        {
            new MainWindow().Show();
            return;
        }

        try
        {
            if (e.Args is ["--install", var install, var stage, var parent, var ticks])
            {
                var work = UpdatePaths.WorkDirectory(install);
                stage = Path.GetFullPath(stage);
                var stageName = Path.GetFileName(stage);
                if (!string.Equals(Path.GetDirectoryName(stage), work, StringComparison.OrdinalIgnoreCase) ||
                    !stageName.StartsWith("stage-", StringComparison.Ordinal) ||
                    !Guid.TryParseExact(stageName[6..], "N", out _) ||
                    File.GetAttributes(stage).HasFlag(FileAttributes.ReparsePoint))
                    throw new InvalidDataException("更新暂存目录无效");
                try
                {
                    UpdateInstallation.Run(install, work, stage,
                        int.Parse(parent, CultureInfo.InvariantCulture), long.Parse(ticks, CultureInfo.InvariantCulture));
                }
                finally
                {
                    try { UpdateInstallation.DeleteDirectory(stage); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            else if (e.Args is ["--recover", var root, var recoveryParent, var recoveryTicks])
            {
                UpdateInstallation.Run(root, UpdatePaths.WorkDirectory(root), null,
                    int.Parse(recoveryParent, CultureInfo.InvariantCulture),
                    long.Parse(recoveryTicks, CultureInfo.InvariantCulture));
            }
            else
            {
                throw new ArgumentException("更新启动参数无效");
            }

            Shutdown();
        }
        catch (Exception ex)
        {
            var details = ex.ToString();
            try
            {
                var report = Path.Combine(AppContext.BaseDirectory, "update-error.txt");
                File.WriteAllText(report, details);
                details = $"{ex.Message}\n\n错误记录：{report}";
            }
            catch (Exception reportError) when (reportError is IOException or UnauthorizedAccessException)
            {
                // Keep the original failure available if the report cannot be written.
            }

            MessageBox.Show($"更新失败：{details}", "SekaiTools", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
