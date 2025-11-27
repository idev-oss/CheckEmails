using System.Collections.Concurrent;
using System.Net.Mail;

namespace CheckEmails.Validation;

/// <summary>
/// Performs syntactic and MX record validation for email addresses.
/// Caches domain validation results to minimize DNS lookups.
/// </summary>
public sealed class EmailValidationService(IMxRecordChecker mxRecordChecker, IEmailDisposableChecker disposableChecker)
{
    // Maximum cache size to prevent unbounded memory growth
    private const int MaxCacheSize = 500000;

    private readonly ConcurrentDictionary<string, Task<EmailDomainStatus>> _domainCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the supplied email and returns details about the outcome.
    /// </summary>
    /// <param name="email">Email address to validate.</param>
    public async Task<EmailValidationResult> ValidateAsync(string email)
    {
        if (!TryGetDomain(email, out var domain))
        {
            return EmailValidationResult.Invalid(EmailRejectionReason.InvalidFormat);
        }

        // Check cache first, with size limit
        var status = await GetOrAddDomainStatusAsync(domain).ConfigureAwait(false);
        return EmailValidationResult.FromDomainStatus(domain, status);
    }

    /// <summary>
    /// Gets cached domain status or adds new one. Clears cache if it exceeds maximum size.
    /// </summary>
    private Task<EmailDomainStatus> GetOrAddDomainStatusAsync(string domain)
    {
        // Clear cache if it's too large to prevent unbounded memory growth
        if (_domainCache.Count >= MaxCacheSize)
        {
            _domainCache.Clear();
        }

        return _domainCache.GetOrAdd(domain, ResolveDomainStatusAsync);
    }

    /// <summary>
    /// Attempts to extract a normalized domain from the supplied email address.
    /// </summary>
    private static bool TryGetDomain(string email, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (!MailAddress.TryCreate(email, out var mailAddress))
        {
            return false;
        }

        domain = mailAddress.Host;
        return true;
    }

    /// <summary>
    /// Computes the validation status for a single domain.
    /// </summary>
    private async Task<EmailDomainStatus> ResolveDomainStatusAsync(string domain)
    {
        if (await disposableChecker.IsDisposableAsync(domain).ConfigureAwait(false))
        {
            return EmailDomainStatus.Invalid(EmailRejectionReason.DisposableDomain);
        }

        var hasMxRecords = await mxRecordChecker.HasMxRecordsAsync(domain).ConfigureAwait(false);

        return hasMxRecords
            ? EmailDomainStatus.Valid
            : EmailDomainStatus.Invalid(EmailRejectionReason.MissingMxRecords);
    }
}