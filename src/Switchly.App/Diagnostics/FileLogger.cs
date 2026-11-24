using System.IO;
using Microsoft.Extensions.Logging;

namespace Switchly.App.Diagnostics;

/// <summary>Rolling per-day file logger (ADR-007: local structured logs, no telemetry).</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _writerDate;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    internal void Write(string category, string message)
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        lock (_lock)
        {
            if (_writer is null || _writerDate != date)
            {
                _writer?.Dispose();
                Directory.CreateDirectory(_directory);
                var path = Path.Combine(_directory, $"switchly-{date}.log");
                _writer = new StreamWriter(path, append: true) { AutoFlush = true };
                _writerDate = date;
            }
            _writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{category}] {message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}

internal sealed class FileLogger : ILogger
{
    private readonly FileLoggerProvider _provider;
    private readonly string _category;

    public FileLogger(FileLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += Environment.NewLine + exception;
        }
        _provider.Write(_category, $"{logLevel}: {message}");
    }
}

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddSwitchlyFile(this ILoggingBuilder builder, string directory)
        => builder.AddProvider(new FileLoggerProvider(directory));
}
