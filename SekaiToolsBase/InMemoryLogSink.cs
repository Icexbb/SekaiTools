using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace SekaiToolsBase;

public class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = "";
    public string Category { get; init; } = "";

    public string LevelText => Level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "?"
    };

    public override string ToString()
        => $"{Timestamp:HH:mm:ss} [{LevelText}] {Category}: {Message}";
}

public class InMemoryLogSink : ILoggerProvider
{
    private static readonly object Lock = new();
    private const int MaxEntries = 2000;

    public static readonly ObservableCollection<LogEntry> Entries = [];

    public ILogger CreateLogger(string categoryName)
        => new InMemoryLogger(categoryName, this);

    public void Dispose() { }

    public static void Clear()
    {
        lock (Lock) Entries.Clear();
    }

    private void AddEntry(LogEntry entry)
    {
        lock (Lock)
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        }
    }

    private class InMemoryLogger(string category, InMemoryLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level = logLevel,
                Category = category,
                Message = exception != null
                    ? $"{formatter(state, exception)}\n{exception}"
                    : formatter(state, exception)
            };

            sink.AddEntry(entry);
        }
    }
}
