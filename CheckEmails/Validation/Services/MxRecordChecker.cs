using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckEmails.Validation;

/// <summary>
/// Performs DNS lookups to determine whether a domain has MX records.
/// Includes rate limiting to avoid overloading DNS servers.
/// </summary>
public sealed class MxRecordChecker : IMxRecordChecker, IDisposable
{
    private readonly Lazy<LookupClient> _lookupClient;
    private readonly MxRecordCheckerOptions _options;
    private readonly ILogger<MxRecordChecker>? _logger;

    // Rate limiting: controls maximum concurrent DNS requests
    private readonly SemaphoreSlim _rateLimiter;
    private bool _disposed;

    public MxRecordChecker() : this(new MxRecordCheckerOptions(), null)
    {
    }

    /// <summary>
    /// Preferred constructor for DI: uses DNS client configured from options with caching enabled.
    /// </summary>
    public MxRecordChecker(IOptions<MxRecordCheckerOptions> options, ILogger<MxRecordChecker> logger)
        : this(options?.Value ?? new MxRecordCheckerOptions(), logger)
    {
    }

    /// <summary>
    /// Creates a checker using the provided options and optional logger.
    /// </summary>
    private MxRecordChecker(MxRecordCheckerOptions options, ILogger<MxRecordChecker>? logger)
    {
        _options = options;
        _logger = logger;

        // Configure DNS client options based on MxRecordCheckerOptions
        var lookupClientOptions = new LookupClientOptions
        {
            Timeout = _options.DnsQueryTimeout,
            Retries = _options.DnsRetries,
            UseCache = true,
            MinimumCacheTimeout = _options.MinimumCacheTimeout
        };

        _lookupClient = new Lazy<LookupClient>(() => new LookupClient(lookupClientOptions));
        _rateLimiter = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    public async Task<bool> HasMxRecordsAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        // Rate limiting: wait for available slot
        await _rateLimiter.WaitAsync().ConfigureAwait(false);
        try
        {
            return await HasMxRecordsCoreAsync(domain).ConfigureAwait(false);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<bool> HasMxRecordsCoreAsync(string domain)
    {
        var attempts = _options.RetryOnTransient ? Math.Max(1, _options.MaxAttempts) : 1;
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                var result = await _lookupClient.Value.QueryAsync(domain, QueryType.MX).ConfigureAwait(false);
                return !result.HasError && result.Answers.MxRecords().Any();
            }
            catch (DnsResponseException ex)
            {
                if (attempt >= attempts)
                {
                    _logger?.LogWarning(ex,
                        "DNS transient failure for {Domain}. Attempts exhausted ({Attempts}). Treating as missing MX.",
                        domain, attempt);
                    return false;
                }

                var delay = GetDelayWithJitter(_options);
                _logger?.LogWarning(ex,
                    "DNS transient failure for {Domain}. Retrying in {Delay} (attempt {Attempt}/{Attempts}).", domain,
                    delay, attempt + 1, attempts);
                await Task.Delay(delay).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "DNS lookup failed for {Domain}. Treating as missing MX.", domain);
                return false;
            }
        }
    }

    private static TimeSpan GetDelayWithJitter(MxRecordCheckerOptions options)
    {
        if (options.JitterSeconds <= 0)
        {
            return options.DelayOnTransient;
        }

        // Use Random.Shared for thread-safe random generation (available in .NET 6+)
        var jitter = Random.Shared.Next(0, options.JitterSeconds + 1);
        return options.DelayOnTransient + TimeSpan.FromSeconds(jitter);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rateLimiter.Dispose();
    }
}