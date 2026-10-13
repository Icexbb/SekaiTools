using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SekaiToolsBase;

public static class Logger
{
    private static ILoggerFactory Factory { get; } = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddConsole();
        builder.AddDebug();
        builder.AddProvider(new InMemoryLogSink());
    });

    private static ConcurrentDictionary<string, ILogger> LoggerDictionary { get; } = new();

    public static ILogger CreateLogger(string categoryName) =>
        LoggerDictionary.GetOrAdd(categoryName, Factory.CreateLogger);

    public static bool IsEnabled(LogLevel logLevel,
        [CallerMemberName] string callerMemberName = "")
    {
        return CreateLogger(callerMemberName).IsEnabled(logLevel);
    }

    public static void Log(string message, LogLevel logLevel = LogLevel.Information,
        [CallerMemberName] string callerMemberName = "")
    {
        CreateLogger(callerMemberName).Log(logLevel, "{Message}", message);
    }
}
