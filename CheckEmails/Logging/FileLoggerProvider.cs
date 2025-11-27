using System.Text;
using Microsoft.Extensions.Logging;

namespace CheckEmails.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public bool IsDebugMode { get; set; }
    public bool UseUtc { get; set; }

    public FileLoggerProvider(LogFileContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Directory.CreateDirectory(context.DirectoryPath);
        var stream = new FileStream(context.FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName, _writer, _lock);

    public void Dispose() => _writer.Dispose();

    private sealed class FileLogger(
        FileLoggerProvider provider,
        string categoryName,
        TextWriter writer,
        object syncRoot)
        : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            var timestamp = provider.UseUtc
                ? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'")
                : DateTime.Now.ToString();

            lock (syncRoot)
            {
                writer.WriteLine($"{timestamp} [{logLevel}] {categoryName}: {message}");
                if (exception is null) return;
                if (provider.IsDebugMode)
                {
                    writer.WriteLine(exception);
                }
                else
                {
                    writer.WriteLine($"{exception.GetType()}: {exception.Message}");
                }
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        private NullScope()
        {
        }

        public void Dispose()
        {
        }
    }
}