namespace CheckEmails.Validation;

/// <summary>
/// Abstraction for DNS MX record lookups to enable testing.
/// </summary>
public interface IMxRecordChecker
{
    /// <summary>
    /// Determines whether the specified domain has MX records.
    /// </summary>
    /// <param name="domain">Domain name to query.</param>
    /// <returns><c>true</c> if MX records are present; otherwise, <c>false</c>.</returns>
    Task<bool> HasMxRecordsAsync(string domain);
}