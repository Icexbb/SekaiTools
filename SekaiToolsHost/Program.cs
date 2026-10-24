using System.Text;
using System.Text.Json;
using SekaiDataFetch.Source;
using SekaiToolsHost;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
var output = Console.Out;
// Shared business logging uses Console. Keep it off the machine-readable stream.
Console.SetOut(Console.Error);
var writeLock = new object();
void Emit(string type, object data)
{
    lock (writeLock)
    {
        output.WriteLine(JsonSerializer.Serialize(new { version = 1, type, data }, Protocol.Json));
        output.Flush();
    }
}

try
{
    var line = await Console.In.ReadLineAsync() ?? throw new InvalidDataException("没有收到请求");
    var request = Protocol.Parse(line);
    using var stop = new CancellationTokenSource();
    void RequestStop()
    {
        try { stop.Cancel(); }
        catch (ObjectDisposedException) { /* The response has already been sent. */ }
    }
    // A child process owns a single operation; EOF means its client has gone away.
    _ = Task.Run(async () =>
    {
        try
        {
            while (await Console.In.ReadLineAsync() is { } control)
            {
                using var doc = JsonDocument.Parse(control);
                if (doc.RootElement.GetProperty("command").GetString() == "cancel") { RequestStop(); return; }
            }
            RequestStop();
        }
        catch (Exception) { RequestStop(); }
    });
    object result;
    switch (request.Command)
    {
        case "capabilities":
            var native = NativeRuntime.Check();
            result = new { subtitleAvailable = native.Available, subtitleReason = native.Reason,
                sources = SourceData.Default.Select((source, id) => new { id, name = source.SourceName }) };
            break;
        case "translation.load": result = TranslationService.Load(Protocol.Arguments<TranslationLoad>(request)); break;
        case "translation.save":
            var save = Protocol.Arguments<TranslationSave>(request);
            Protocol.RejectInputOverwrite(save.Output, save.Script);
            Protocol.AtomicWrite(save.Output, TranslationService.Export(save));
            result = new { output = save.Output };
            break;
        case "download.catalog": result = await DownloadService.Catalog(Protocol.Arguments<CatalogArgs>(request)); break;
        case "download.save":
            var download = Protocol.Arguments<DownloadArgs>(request);
            await DownloadService.Save(download);
            result = new { output = download.Output };
            break;
        case "subtitle.run": result = await SubtitleService.Run(Protocol.Arguments<SubtitleArgs>(request), data => Emit("progress", data), stop.Token); break;
        default: throw new InvalidDataException("未知命令");
    }
    Emit("result", result);
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Emit("error", new { message = error.Message });
    Environment.ExitCode = 1;
}
