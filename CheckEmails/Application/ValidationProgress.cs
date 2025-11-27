using System.Diagnostics;
using CheckEmails.Validation;

namespace CheckEmails.Application;

/// <summary>
/// Tracks cumulative validation statistics for progress reporting and summaries.
/// </summary>
internal sealed class ValidationProgress
{
    private readonly int _expectedTotal;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private int _processed;
    private int _valid;
    private int _invalidDisposable;
    private int _invalidMissingMx;
    private int _invalidFormat;
    private TimeSpan? _frozenElapsed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationProgress"/> class.
    /// </summary>
    public ValidationProgress(int expectedTotal)
    {
        _expectedTotal = expectedTotal < 0 ? 0 : expectedTotal;
    }

    /// <summary>
    /// Stops the internal stopwatch and freezes the elapsed time.
    /// Call this when validation processing is complete to prevent throughput fluctuations.
    /// </summary>
    public void StopTiming()
    {
        if (_frozenElapsed is null)
        {
            _frozenElapsed = _stopwatch.Elapsed;
            _stopwatch.Stop();
        }
    }

    /// <summary>
    /// Produces a snapshot of the current progress state.
    /// </summary>
    public ValidationProgressSnapshot GetSnapshot()
    {
        return new ValidationProgressSnapshot(
            _expectedTotal,
            Volatile.Read(ref _processed),
            Volatile.Read(ref _valid),
            Volatile.Read(ref _invalidDisposable),
            Volatile.Read(ref _invalidMissingMx),
            Volatile.Read(ref _invalidFormat),
            _frozenElapsed ?? _stopwatch.Elapsed);
    }

    /// <summary>
    /// Records the outcome of a single email validation.
    /// </summary>
    public void RecordResult(EmailValidationResult result)
    {
        Interlocked.Increment(ref _processed);

        if (result.IsValid)
        {
            Interlocked.Increment(ref _valid);
            return;
        }

        switch (result.Reason)
        {
            case EmailRejectionReason.DisposableDomain:
                Interlocked.Increment(ref _invalidDisposable);
                break;
            case EmailRejectionReason.MissingMxRecords:
                Interlocked.Increment(ref _invalidMissingMx);
                break;
            case EmailRejectionReason.InvalidFormat:
            case EmailRejectionReason.None:
            default:
                Interlocked.Increment(ref _invalidFormat);
                break;
        }
    }
}