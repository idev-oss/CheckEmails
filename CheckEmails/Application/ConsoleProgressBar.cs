using System.Text;

namespace CheckEmails.Application;

/// <summary>
/// Renders a throttled progress indicator for validation runs.
/// </summary>
internal sealed class ConsoleProgressBar : IDisposable
{
    private const int LineCount = 3;
    private readonly ValidationProgress _progress;
    private readonly int _barWidth;
    private readonly TimeSpan _minUpdateInterval;

    private bool _completed;
    private int _cursorTop;
    private bool _cursorInitialized;

    private DateTime _lastRenderUtc;
    private ValidationProgressSnapshot _lastSnapshot;
    private int _lastBlocks = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleProgressBar"/> class.
    /// </summary>
    public ConsoleProgressBar(ValidationProgress progress, int barWidth = 28, int minUpdateIntervalMs = 75)
    {
        _progress = progress;
        _barWidth = Math.Clamp(barWidth, 10, 80);
        _minUpdateInterval = TimeSpan.FromMilliseconds(Math.Clamp(minUpdateIntervalMs, 16, 1000));
    }

    /// <summary>
    /// Refreshes the console output when progress has changed.
    /// </summary>
    public void Refresh(bool force = false)
    {
        if (_completed && !force)
        {
            return;
        }

        var snapshot = _progress.GetSnapshot();
        var now = DateTime.UtcNow;

        var totalForCalc = snapshot.ExpectedTotal > 0 ? snapshot.ExpectedTotal : Math.Max(snapshot.Processed, 1);
        int blocks;
        int percent;
        if (snapshot.ExpectedTotal > 0)
        {
            blocks = (int)Math.Min(_barWidth, ((long)snapshot.Processed * _barWidth) / totalForCalc);
            percent = (int)Math.Min(100, ((long)snapshot.Processed * 100) / totalForCalc);
        }
        else
        {
            blocks = snapshot.Processed == 0 ? 0 : _barWidth;
            percent = snapshot.Processed == 0 ? 0 : 100;
        }

        if (!force &&
            (now - _lastRenderUtc) < _minUpdateInterval &&
            snapshot.Processed == _lastSnapshot.Processed &&
            snapshot.Valid == _lastSnapshot.Valid &&
            snapshot.Invalid == _lastSnapshot.Invalid &&
            blocks == _lastBlocks)
        {
            return;
        }

        var sb = new StringBuilder(128);

        // Line 1: Progress bar
        sb.Append("Processing ");
        sb.Append(snapshot.Processed);
        sb.Append('/');
        sb.Append(snapshot.ExpectedTotal > 0 ? snapshot.ExpectedTotal : snapshot.Processed);
        sb.Append(" [");
        sb.Append('#', blocks);
        sb.Append('-', _barWidth - blocks);
        sb.Append("] ");
        sb.Append(percent.ToString().PadLeft(3));
        sb.Append('%');
        var line1 = sb.ToString();
        sb.Clear();

        // Line 2: Stats
        sb.Append("valid: ");
        sb.Append(snapshot.Valid);
        sb.Append(" invalid: ");
        sb.Append(snapshot.Invalid);
        sb.Append(" (disp: ");
        sb.Append(snapshot.InvalidDisposable);
        sb.Append(" mx: ");
        sb.Append(snapshot.InvalidMissingMx);
        sb.Append(" fmt: ");
        sb.Append(snapshot.InvalidFormat);
        sb.Append(')');
        var line2 = sb.ToString();
        sb.Clear();
        
        // Line 3: Timings
        string line3 = string.Empty;
        if (snapshot.Elapsed.TotalSeconds >= 1)
        {
            sb.Append("Elapsed: ");
            sb.Append(snapshot.Elapsed.ToString(@"hh\:mm\:ss"));

            if (snapshot.Throughput > 0.1)
            {
                sb.Append(" rate: ");
                sb.Append(snapshot.Throughput.ToString("0.0"));
                sb.Append("/s");
            }
            line3 = sb.ToString();
        }

        Write(new[] { line1, line2, line3 });

        _lastRenderUtc = now;
        _lastSnapshot = snapshot;
        _lastBlocks = blocks;
    }

    /// <summary>
    /// Writes the provided message to the console using a single, atomic write operation to prevent flickering.
    /// </summary>
    private void Write(string[] lines)
    {
        const string clearLineSequence = "\x1b[2K";
        
        try
        {
            if (!_cursorInitialized)
            {
                // Reserve space for the progress bar and hide the cursor.
                for (int i = 0; i < LineCount; i++)
                {
                    Console.WriteLine();
                }
                _cursorTop = Console.CursorTop - LineCount;
                _cursorInitialized = true;
                Console.CursorVisible = false;
            }

            var sb = new StringBuilder(256);
            int width = Console.WindowWidth;

            for (int i = 0; i < lines.Length; i++)
            {
                // ANSI code to move cursor to the specific line and column 1.
                // Note: ANSI cursor positions are 1-based.
                sb.Append($"\x1b[{_cursorTop + i + 1};1H");
                
                // ANSI code to clear the entire line.
                sb.Append(clearLineSequence);

                // Truncate and append the actual content.
                string output = lines[i];
                if (width > 0 && output.Length >= width)
                {
                    output = output.Substring(0, width - 1);
                }
                sb.Append(output);
            }
            
            // Perform a single write operation for the entire block.
            Console.Write(sb.ToString());
        }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentOutOfRangeException)
        {
            // Fallback for non-interactive consoles.
            var singleLine = string.Join(" | ", lines);
            Console.Write("\r" + singleLine + "   ");
        }
    }


    /// <summary>
    /// Finalizes the progress display and emits a trailing newline.
    /// </summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        Refresh(force: true);
        _completed = true;

        try
        {
            if (_cursorInitialized)
            {
                // Position the cursor after the progress bar block and make it visible again.
                Console.SetCursorPosition(0, _cursorTop + LineCount);
                Console.CursorVisible = true;
            }
        }
        catch (Exception ex) when (ex is System.IO.IOException or ArgumentOutOfRangeException)
        {
            // Ignore if not an interactive console or if positioning fails.
        }
        
        // Ensure we always end with a newline.
        Console.WriteLine();
        _cursorInitialized = false;
    }

    public void Dispose()
    {
        Complete();
    }
}
