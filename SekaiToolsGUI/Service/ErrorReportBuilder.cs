using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SekaiToolsBase;

namespace SekaiToolsGUI.Service;

internal static class ErrorReportBuilder
{
    private const int RecentLogCount = 100;

    public static string Build(Exception exception, string source, DateTimeOffset occurredAt)
    {
        var report = new StringBuilder();
        report.AppendLine("Sekai Tools 错误报告");
        report.AppendLine($"发生时间: {occurredAt:yyyy-MM-dd HH:mm:ss zzz}");
        report.AppendLine($"异常来源: {source}");
        report.AppendLine($"应用版本: {GetAppVersion()}");
        report.AppendLine($"运行环境: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine($"操作系统: {RuntimeInformation.OSDescription}");
        report.AppendLine($"进程架构: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine();
        report.AppendLine("异常详情");
        report.AppendLine(exception.ToString());

        var logs = InMemoryLogSink.Snapshot().TakeLast(RecentLogCount).ToArray();
        if (logs.Length == 0) return report.ToString().TrimEnd();

        report.AppendLine();
        report.AppendLine($"最近运行日志（最多 {RecentLogCount} 条）");
        foreach (var entry in logs)
            report.AppendLine(entry.ToString());

        return report.ToString().TrimEnd();
    }

    private static string GetAppVersion()
    {
        var path = Assembly.GetEntryAssembly()?.Location;
        if (path == null || !File.Exists(path)) return "未知";
        return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "未知";
    }
}
