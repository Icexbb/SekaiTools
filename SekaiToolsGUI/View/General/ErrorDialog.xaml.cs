using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using SekaiToolsConfiguration;
using SekaiToolsGUI.Service;
using Wpf.Ui.Controls;

namespace SekaiToolsGUI.View.General;

public partial class ErrorDialog : FluentWindow
{
    private readonly Exception _exception;
    private readonly string _report;

    public ErrorDialog(Exception exception, string source, bool isTerminating)
    {
        InitializeComponent();

        _exception = exception;
        _report = ErrorReportBuilder.Build(exception, source, DateTimeOffset.Now);
        SummaryText.Text = string.IsNullOrWhiteSpace(exception.Message)
            ? "程序发生了未预期的错误。"
            : exception.Message;
        SuggestionText.Text = BuildSuggestion(exception, isTerminating);
        DetailsTextBox.Text = _report;
    }

    private static string BuildSuggestion(Exception exception, bool isTerminating)
    {
        var specificSuggestion = exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                "请确认相关文件没有被移动或删除，并重新选择正确的文件路径。",
            UnauthorizedAccessException =>
                "请确认文件没有被其他程序占用，并检查当前账户是否拥有读写权限。",
            HttpRequestException or TaskCanceledException or TimeoutException =>
                "请检查网络连接与代理设置，然后重试当前操作。",
            InvalidDataException =>
                "输入文件或下载的数据可能已损坏，请重新获取后再试。",
            OutOfMemoryException =>
                "系统可用内存不足。请关闭其他大型程序，重新启动 Sekai Tools 后再试。",
            _ => "请先重试刚才的操作。若问题重复出现，请复制错误报告并提交 GitHub Issue。"
        };

        var reportHint = "错误报告包含异常堆栈、运行环境和最近 100 条日志；提交前请检查其中是否含有不希望公开的本地路径。";
        return isTerminating
            ? $"{specificSuggestion}\n\n此错误导致程序无法继续运行，关闭本窗口后 Sekai Tools 将退出。\n{reportHint}"
            : $"{specificSuggestion}\n\n{reportHint}";
    }

    private void CopyButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_report);
            CopyButton.Content = "已复制";
        }
        catch
        {
            CopyButton.Content = "复制失败，请展开详细信息手动复制";
            DetailsExpander.IsExpanded = true;
        }
    }

    private void OpenIssueButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var title = Uri.EscapeDataString($"[Bug] {_exception.GetType().Name}: {TrimTitle(_exception.Message)}");
            var issueUrl = $"{NetworkEndpoints.RepositoryUrl.TrimEnd('/')}/issues/new?title={title}";
            Process.Start(new ProcessStartInfo(issueUrl) { UseShellExecute = true });
        }
        catch
        {
            if (sender is Button button) button.Content = "无法打开，请先复制报告";
        }
    }

    private static string TrimTitle(string value)
    {
        const int maxLength = 80;
        var singleLine = value.ReplaceLineEndings(" ").Trim();
        if (string.IsNullOrEmpty(singleLine)) return "未预期错误";
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
