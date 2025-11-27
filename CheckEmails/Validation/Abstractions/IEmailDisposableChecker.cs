namespace CheckEmails.Validation;

/// <summary>
/// Determines whether a domain belongs to a disposable email provider.
/// </summary>
public interface IEmailDisposableChecker
{
    /// <summary>
    /// Returns <c>true</c> when the provided domain is considered disposable.
    /// </summary>
    Task<bool> IsDisposableAsync(string domain);
}