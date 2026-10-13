using Microsoft.Extensions.Logging;

namespace SekaiToolsGUI;

internal static class Log
{
    public static ILogger Logger { get; } = SekaiToolsBase.Logger.CreateLogger("SekaiToolsGUI");
}
