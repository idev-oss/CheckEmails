using System.Runtime.CompilerServices;
using System.Text;

namespace CheckEmails.IO;

/// <summary>
/// Provides asynchronous streaming access to email addresses stored in a file.
/// </summary>
public static class EmailFileReader
{
    /// <summary>
    /// Reads email addresses asynchronously from the specified file, splitting comma-delimited entries.
    /// </summary>
    /// <param name="filePath">Path to the source file.</param>
    /// <param name="cancellationToken">Cancellation token used to cancel the read operation.</param>
    /// <returns>An asynchronous stream of email addresses.</returns>
    public static async IAsyncEnumerable<string> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in File.ReadLinesAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            foreach (var email in EnumerateLine(line))
            {
                yield return email;
            }
        }
    }

    /// <summary>
    /// Counts the number of non-empty email entries in the specified file, including comma-delimited entries.
    /// Uses an allocation-free per-line counter for performance.
    /// Note: This reads the entire file. Consider using EstimateCount for progress indication.
    /// </summary>
    public static async Task<int> CountAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var line in File.ReadLinesAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            count += CountTokens(line.AsSpan());
        }

        return count;
    }

    /// <summary>
    /// Reads and counts email entries in a single pass.
    /// Returns both the count and the enumerable of emails.
    /// </summary>
    public static async Task<(int Count, List<string> Emails)> ReadAllWithCountAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var emails = new List<string>();

        await foreach (var line in File.ReadLinesAsync(filePath, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            emails.AddRange(EnumerateLine(line));
        }

        return (emails.Count, emails);
    }

    /// <summary>
    /// Splits a single line by commas and yields trimmed, non-empty tokens.
    /// Guarantees: returned emails have no leading/trailing whitespace and contain no commas.
    /// </summary>
    private static IEnumerable<string> EnumerateLine(string line)
    {
        var start = 0;
        var length = line.Length;

        for (var i = 0; i <= length; i++)
        {
            var isSeparator = i == length || line[i] == ',';
            if (!isSeparator) continue;

            var left = start;
            var right = i - 1;

            while (left <= right && char.IsWhiteSpace(line[left])) left++;
            while (right >= left && char.IsWhiteSpace(line[right])) right--;

            if (left <= right)
            {
                yield return line.Substring(left, right - left + 1);
            }

            start = i + 1;
        }
    }

    private static int CountTokens(ReadOnlySpan<char> span)
    {
        var count = 0;
        var start = 0;
        var length = span.Length;

        for (var i = 0; i <= length; i++)
        {
            var isSeparator = i == length || span[i] == ',';
            if (!isSeparator) continue;

            var left = start;
            var right = i - 1;

            while (left <= right && char.IsWhiteSpace(span[left])) left++;
            while (right >= left && char.IsWhiteSpace(span[right])) right--;

            if (left <= right)
            {
                count++;
            }

            start = i + 1;
        }

        return count;
    }
}