using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace SekaiToolsBase;

public static class Logger
{
    private static ILoggerFactory Factory { get; } = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Information);
    });

    private static Dictionary<string, ILogger> LoggerDictionary { get; } = new();

    public static void Log(string message, LogLevel logLevel = LogLevel.Information,
        [CallerMemberName] string callerMemberName = "")
    {
        if (!LoggerDictionary.ContainsKey(callerMemberName))
            LoggerDictionary[callerMemberName] = Factory.CreateLogger(callerMemberName);
        var logger = LoggerDictionary[callerMemberName];


        switch (logLevel)
        {
            case LogLevel.Trace:
                logger.LogTrace(message);
                break;
            case LogLevel.Debug:
                logger.LogDebug(message);
                break;

            case LogLevel.Warning:
                logger.LogWarning(message);
                break;
            case LogLevel.Error:
                logger.LogError(message);
                break;
            case LogLevel.Critical:
                logger.LogCritical(message);
                break;
            case LogLevel.None:
                break;
            case LogLevel.Information:
            default:
                logger.LogInformation(message);
                break;
        }
    }
}