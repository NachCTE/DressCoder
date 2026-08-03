using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DressCoder.Infrastructure.Logging;

/// <summary>
/// Minimal rolling-file logger provider for a portable (no-installer) app: writes to
/// {logDirectory}/dresscoder-{yyyy-MM-dd}.log next to the executable, no external logging
/// framework dependency (Serilog/NLog) needed for this simple use case. Thread-safe via a
/// per-provider lock; one file per calendar day.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logDirectory, LogLevel minimumLevel = LogLevel.Information)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _minimumLevel, WriteLine));

    private void WriteLine(string line)
    {
        var path = Path.Combine(_logDirectory, $"dresscoder-{DateTime.Now:yyyy-MM-dd}.log");
        lock (_writeLock)
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }

    public void Dispose() => _loggers.Clear();

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogLevel _minimumLevel;
        private readonly Action<string> _write;

        public FileLogger(string categoryName, LogLevel minimumLevel, Action<string> write)
        {
            _categoryName = categoryName;
            _minimumLevel = minimumLevel;
            _write = write;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{LevelLabel(logLevel)}] {_categoryName}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            _write(line);
        }

        private static string LevelLabel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRCE",
            LogLevel.Debug => "DBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "FAIL",
            LogLevel.Critical => "CRIT",
            _ => "????",
        };
    }
}
