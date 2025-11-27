namespace CheckEmails.Validation;

/// <summary>
/// Describes the cached validation result for a particular email domain.
/// </summary>
internal readonly record struct EmailDomainStatus(bool IsValid, EmailRejectionReason Reason)
{
    /// <summary>
    /// Gets a status indicating that the domain is valid.
    /// </summary>
    public static EmailDomainStatus Valid { get; } = new(true, EmailRejectionReason.None);

    /// <summary>
    /// Creates an invalid domain status with the supplied reason.
    /// </summary>
    public static EmailDomainStatus Invalid(EmailRejectionReason reason) => new(false, reason);
}