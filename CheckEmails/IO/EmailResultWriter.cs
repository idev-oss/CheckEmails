using System.Text;

namespace CheckEmails.IO;

/// <summary>
/// Writes validated email addresses to the designated result files.
/// </summary>
public sealed class EmailResultWriter : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private EmailResultWriter(StreamWriter writer)
    {
        _writer = writer;
    }

    public static EmailResultWriter Create(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, Encoding.UTF8);
        return new EmailResultWriter(writer);
    }

    public async Task WriteAsync(string email)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(email).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await _writer.FlushAsync().ConfigureAwait(false);
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
            _writeLock.Dispose();
        }
    }
}