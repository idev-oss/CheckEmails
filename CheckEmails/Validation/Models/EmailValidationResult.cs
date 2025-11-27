namespace CheckEmails.Validation;

/// <summary>
/// Represents the outcome of a single email validation.
/// </summary>
public readonly record struct EmailValidationResult(bool IsValid, EmailRejectionReason Reason, string? Domain)
{
    /// <summary>
    /// Creates a successful validation result for the specified domain.
    /// </summary>
    public static EmailValidationResult Valid(string domain) => new(true, EmailRejectionReason.None, domain);

    /// <summary>
    /// Creates an invalid validation result with the supplied reason.
    /// </summary>
    public static EmailValidationResult Invalid(EmailRejectionReason reason, string? domain = null) =>
        new(false, reason, domain);

    /// <summary>
    /// Translates a cached domain status into a validation result.
    /// </summary>
    internal static EmailValidationResult FromDomainStatus(string domain, EmailDomainStatus status) =>
        status.IsValid ? Valid(domain) : Invalid(status.Reason, domain);
}