namespace CheckEmails.Validation;

/// <summary>
/// Provides retry and rate limiting configuration for DNS MX lookups performed by <see cref="MxRecordChecker"/>.
/// Properties use 'set' instead of 'init' for AOT-compatible manual configuration binding.
/// </summary>
public sealed class MxRecordCheckerOptions
{
    /// <summary>
    /// Gets or sets whether to retry on transient DNS failures (e.g., timeouts).
    /// </summary>
    public bool RetryOnTransient { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of attempts for a single domain lookup, including the initial try.
    /// </summary>
    public int MaxAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the base delay between attempts when a transient <c>DnsResponseException</c> occurs.
    /// </summary>
    public TimeSpan DelayOnTransient { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the number of seconds of random jitter to add to the retry delay to avoid thundering herds.
    /// </summary>
    public int JitterSeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of concurrent DNS requests to prevent overloading DNS servers.
    /// Default is 10 which provides good balance between throughput and DNS server load.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the minimum cache timeout for MX record lookups.
    /// Longer cache times reduce DNS queries and memory churn, improving performance within 512MB budget.
    /// Default is 30 minutes.
    /// </summary>
    public TimeSpan MinimumCacheTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the DNS query timeout.
    /// Default is 3 seconds.
    /// </summary>
    public TimeSpan DnsQueryTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the number of DNS query retries at the client level.
    /// Default is 1.
    /// </summary>
    public int DnsRetries { get; set; } = 1;
}