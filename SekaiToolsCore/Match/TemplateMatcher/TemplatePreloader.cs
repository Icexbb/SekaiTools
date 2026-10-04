using System.Threading.Channels;
using SekaiToolsBase;
using SekaiToolsBase.Story.StoryEvent;
using SekaiToolsCore.Process.Model;

namespace SekaiToolsCore.Match.TemplateMatcher;

internal sealed class TemplatePreloader : IDisposable
{
    private const long Budget = 16L * 1024 * 1024;
    private static readonly double[] Scales = [1.0, 0.96, 1.04];
    private readonly TemplateManager _target;
    private readonly TemplateManager _builder;
    private readonly DialogStoryEvent[] _dialogs;
    private readonly BannerStoryEvent[] _banners;
    private readonly MarkerStoryEvent[] _markers;
    private readonly CancellationTokenSource _stop;
    private readonly Channel<(int Dialog, int Banner, int Marker)> _requests =
        Channel.CreateBounded<(int, int, int)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Task _worker;
    private readonly int _fontSize;
    private (int, int, int)? _last;
    private long _bytes;
    private bool _disposed;

    internal TemplatePreloader(TemplateManager target, TemplateManager builder, int fontSize,
        DialogStoryEvent[] dialogs, BannerStoryEvent[] banners, MarkerStoryEvent[] markers, CancellationToken token)
    {
        _target = target; _builder = builder; _fontSize = fontSize;
        _dialogs = dialogs; _banners = banners; _markers = markers;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _worker = Task.Run(RunAsync);
    }

    internal void Request(int dialog, int banner, int marker)
    {
        var request = (dialog, banner, marker);
        if (_last == request) return;
        _last = request;
        _requests.Writer.TryWrite(request);
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(_stop.Token))
            {
                foreach (var dialog in Window(_dialogs, request.Dialog))
                {
                    Prepare(TemplateUsage.DialogNameTag, DialogTemplateMatcher.TrimTemplateContent(dialog.CharacterOriginal));
                    for (var length = 1; length <= Math.Min(3, dialog.BodyOriginal.Length); length++)
                        Prepare(TemplateUsage.DialogContent, dialog.BodyOriginal[..length]);
                }
                foreach (var banner in Window(_banners, request.Banner))
                    Prepare(TemplateUsage.BannerContent, BannerTemplateMatcher.TrimContent(banner.BodyOriginal));
                foreach (var marker in Window(_markers, request.Marker))
                {
                    Prepare(TemplateUsage.MarkerContent, marker.BodyOriginal, true);
                    if (marker.BodyOriginal.Length > 0) Prepare(TemplateUsage.MarkerContent, marker.BodyOriginal[^1].ToString(), true);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error) { Logger.Log($"模板预生成已停止，识别继续按需生成: {error.Message}"); }
    }

    private static IEnumerable<T> Window<T>(T[] items, int index) => index < 0 ? [] : items.Skip(index).Take(4);

    private void Prepare(TemplateUsage usage, string text, bool marker = false)
    {
        _stop.Token.ThrowIfCancellationRequested();
        // Bound transient high-resolution canvases as well as published cache bytes.
        if (string.IsNullOrWhiteSpace(text) || _bytes >= Budget || text.Length > 32
            || 64L * text.Length * _fontSize * _fontSize > Budget
            || _target.HasMatchTemplate(usage, text, marker)) return;
        GaMat? template = null;
        try
        {
            if (marker) template = _builder.CreateMarkerMatchTemplate(text);
            else
            {
                using var source = _builder.CreateTemplate(usage, text);
                template = new GaMat(source);
            }
            long bytes = (long)template.Gray.Rows * template.Gray.Cols * 2;
            foreach (var scale in Scales)
                foreach (var divisor in new[] { 1, 2 })
                {
                    var layer = template.GetScaledLayer(scale, divisor);
                    if (!ReferenceEquals(layer.Gray, template.Gray)) bytes += (long)layer.Gray.Rows * layer.Gray.Cols * 2;
                }
            if (_bytes + bytes > Budget) return;
            _stop.Token.ThrowIfCancellationRequested();
            if (ReferenceEquals(_target.Publish(usage, text, template, marker), template)) _bytes += bytes;
            template = null; // Publish either owns it or disposed a duplicate.
        }
        finally { template?.Dispose(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _stop.Cancel();
        _worker.GetAwaiter().GetResult();
        _builder.Dispose();
        _stop.Dispose();
        _disposed = true;
    }
}
