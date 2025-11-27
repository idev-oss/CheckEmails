namespace CheckEmails.Application;

/// <summary>
/// Captures a read-only view of progress metrics for an email validation run.
/// </summary>
internal readonly record struct ValidationProgressSnapshot(
    int ExpectedTotal,
    int Processed,
    int Valid,
    int InvalidDisposable,
    int InvalidMissingMx,
    int InvalidFormat,
    TimeSpan Elapsed)
{
    /// <summary>
    /// Gets the total number of invalid addresses encountered.
    /// </summary>
    public int Invalid => InvalidDisposable + InvalidMissingMx + InvalidFormat;

    /// <summary>
    /// Gets the estimated number of remaining entries when a total is known.
    /// </summary>
    public int Remaining => ExpectedTotal > 0 ? Math.Max(ExpectedTotal - Processed, 0) : 0;

    /// <summary>
    /// Gets the percentage of work completed as a value between 0 and 1.
    /// </summary>
    public double PercentComplete
    {
        get
        {
            if (ExpectedTotal > 0)
            {
                return Math.Clamp(Processed / (double)ExpectedTotal, 0d, 1d);
            }

            return Processed == 0 ? 0d : 1d;
        }
    }

    /// <summary>
    /// Gets the processed items per second based on elapsed time.
    /// </summary>
    public double Throughput =>
        Elapsed.TotalSeconds <= 0 ? 0 : Processed / Elapsed.TotalSeconds;
}