namespace CheckEmails.Validation;

/// <summary>
/// Enumerates reasons an email address failed validation.
/// </summary>
public enum EmailRejectionReason
{
    None = 0,
    InvalidFormat,
    DisposableDomain,
    MissingMxRecords
}