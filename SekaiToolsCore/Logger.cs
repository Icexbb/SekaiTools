using Microsoft.Extensions.Logging;

namespace SekaiToolsCore;

internal static class Log
{
    private static ILoggerFactory Factory { get; } = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    public static ILogger Logger { get; } = Factory.CreateLogger("SekaiToolsCore");
}