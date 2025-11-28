using System.Collections.Frozen;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckEmails.Validation;

/// <summary>Provides disposable-domain lookups backed by remote and custom lists.</summary>
public sealed class DisposableEmailChecker(
    IOptions<DisposableEmailCheckerOptions> options,
    ILogger<DisposableEmailChecker> logger,
    HttpClient httpClient) : IEmailDisposableChecker, IDisposableEmailSourceConfigurator, IDisposable
{
    private readonly DisposableEmailCheckerOptions _options = options.Value;
    private readonly string _remoteFilePath = Path.Combine(options.Value.RootDirectory, options.Value.RemoteFileName);

    private readonly string _defaultCustomFilePath =
        Path.Combine(options.Value.RootDirectory, options.Value.CustomFileName);

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Maximum cache size to prevent unbounded memory growth
    private const int MaxCacheSize = 500000;

    private string? _customFilePath;
    private readonly MemoryCache _resultCache = new(new MemoryCacheOptions { SizeLimit = MaxCacheSize });
    private Task<FrozenSet<string>>? _domainSetTask;
    private bool _initialized;
    private bool _disposed;

    public async Task InitializeAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await InitializeCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Core initialization logic without locking. Must be called within semaphore.
    /// </summary>
    private async Task InitializeCoreAsync()
    {
        if (_initialized) return;

        Directory.CreateDirectory(_options.RootDirectory);
        await EnsureDefaultCustomFileAsync().ConfigureAwait(false);
        _initialized = true;
    }

    public async Task<bool> IsDisposableAsync(string domain)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var domains = await GetDomainsAsync().ConfigureAwait(false);
        if (domains.Count == 0 || string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var normalized = domain.AsSpan().Trim().TrimEnd('.');
        if (normalized.IsEmpty)
        {
            return false;
        }

        var domainKey = new string(normalized);
        if (_resultCache.TryGetValue(domainKey, out var cachedObj) && cachedObj is bool cached)
        {
            return cached;
        }

        // Check if domain or any parent domain is in the blocklist
        var result = CheckDomainInSet(normalized, domains);

        // Add to cache with size limit check
        TryAddToCache(domainKey, result);
        return result;
    }

    /// <summary>
    /// Checks if domain or any of its parent domains exists in the blocklist.
    /// Optimized to minimize string allocations.
    /// </summary>
    private static bool CheckDomainInSet(ReadOnlySpan<char> domain, FrozenSet<string> domains)
    {
        var current = domain;

        // Stack-allocate buffer for domain comparisons (max domain length is 253 chars)
        Span<char> buffer = stackalloc char[256];

        while (!current.IsEmpty)
        {
            if (current.Length <= buffer.Length)
            {
                current.CopyTo(buffer);
                var domainStr = new string(buffer[..current.Length]);
                if (domains.Contains(domainStr))
                {
                    return true;
                }
            }

            var dotIndex = current.IndexOf('.');
            if (dotIndex < 0)
            {
                break;
            }

            current = current[(dotIndex + 1)..];
        }

        return false;
    }

    /// <summary>
    /// Adds result to cache, clearing cache if it exceeds maximum size.
    /// </summary>
    private void TryAddToCache(string domainKey, bool result)
    {
        _resultCache.Set(domainKey, result, new MemoryCacheEntryOptions { Size = 1 });
    }

    public async Task SetCustomFileAsync(string? path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Initialize without acquiring semaphore again (we already hold it)
            await InitializeCoreAsync().ConfigureAwait(false);

            _customFilePath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
            if (!string.IsNullOrWhiteSpace(_customFilePath) && !File.Exists(_customFilePath))
            {
                logger.LogWarning("Custom disposable domain file not found: {Path}", _customFilePath);
            }

            ResetDomainCache();
        }
        finally
        {
            _semaphore.Release();
        }

        // Warm up domain cache to ensure custom list is loaded before validation starts
        await GetDomainsAsync().ConfigureAwait(false);
    }

    public async Task RefreshAsync(bool forceDownload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            // Initialize without acquiring semaphore again (we already hold it)
            await InitializeCoreAsync().ConfigureAwait(false);
            ResetDomainCache(forceDownload);
        }
        finally
        {
            _semaphore.Release();
        }

        // Force loading the latest list into memory before validation begins
        await GetDomainsAsync(forceDownload).ConfigureAwait(false);
    }

    public async Task DownloadRemoteListAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await EnsureRemoteListAsync(forceDownload: true).ConfigureAwait(false);
    }

    private Task<FrozenSet<string>> GetDomainsAsync(bool forceDownload = false)
    {
        var existingTask = _domainSetTask;
        if (!forceDownload && existingTask is not null)
        {
            return existingTask;
        }

        return GetDomainsWithLockAsync(forceDownload);
    }

    private async Task<FrozenSet<string>> GetDomainsWithLockAsync(bool forceDownload)
    {
        Task<FrozenSet<string>> loadTask;

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!forceDownload && _domainSetTask is not null)
            {
                loadTask = _domainSetTask;
            }
            else
            {
                loadTask = ReloadDomainsUnsafeAsync(forceDownload);
                _domainSetTask = loadTask;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return await loadTask.ConfigureAwait(false);
    }

    private void ResetDomainCache(bool forceDownload = false)
    {
        _domainSetTask = ReloadDomainsUnsafeAsync(forceDownload);
        _resultCache.Compact(1.0);
    }

    private async Task<FrozenSet<string>> ReloadDomainsUnsafeAsync(bool forceDownload)
    {
        await EnsureRemoteListAsync(forceDownload).ConfigureAwait(false);

        var builder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await AppendDomainsFromFileAsync(_remoteFilePath, builder).ConfigureAwait(false);
        await AppendDomainsFromFileAsync(_defaultCustomFilePath, builder).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_customFilePath))
        {
            await AppendDomainsFromFileAsync(_customFilePath!, builder).ConfigureAwait(false);
        }

        return builder.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task EnsureRemoteListAsync(bool forceDownload)
    {
        var fileInfo = new FileInfo(_remoteFilePath);
        var shouldRefresh = forceDownload || !fileInfo.Exists ||
                            (DateTime.UtcNow - fileInfo.LastWriteTimeUtc) >= _options.RefreshInterval;

        if (!shouldRefresh) return;

        try
        {
            var content = await httpClient.GetStringAsync(_options.RemoteListUri).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("Received empty disposable domain list from {Uri}.", _options.RemoteListUri);
                return;
            }

            await File.WriteAllTextAsync(_remoteFilePath, content, Encoding.UTF8).ConfigureAwait(false);
            File.SetLastWriteTimeUtc(_remoteFilePath, DateTime.UtcNow);
            logger.LogInformation("Disposable domain list refreshed from {Uri}.", _options.RemoteListUri);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to refresh disposable domain list from {Uri}. Using cached copy if available.",
                _options.RemoteListUri);
        }
    }

    private static async Task AppendDomainsFromFileAsync(string path, HashSet<string> set)
    {
        if (!File.Exists(path)) return;

        await foreach (var line in File.ReadLinesAsync(path, Encoding.UTF8).ConfigureAwait(false))
        {
            var span = line.AsSpan().Trim();
            if (!span.IsEmpty && !span.StartsWith("#") && !span.StartsWith("//"))
            {
                set.Add(new string(span));
            }
        }
    }

    private async Task EnsureDefaultCustomFileAsync()
    {
        if (File.Exists(_defaultCustomFilePath)) return;
        var header = "# Add one disposable domain per line" + Environment.NewLine;
        await File.WriteAllTextAsync(_defaultCustomFilePath, header, Encoding.UTF8).ConfigureAwait(false);
        logger.LogInformation("Created default custom disposable domain list at {Path}", _defaultCustomFilePath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _resultCache.Dispose();
        _semaphore.Dispose();
    }
}