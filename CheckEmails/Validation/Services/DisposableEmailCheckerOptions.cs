namespace CheckEmails.Validation;

/// <summary>
/// Provides configuration values for the <see cref="DisposableEmailChecker"/>.
/// Properties use 'set' instead of 'init' for AOT-compatible manual configuration binding.
/// </summary>
public sealed class DisposableEmailCheckerOptions
{
    /// <summary>
    /// Gets or sets the base directory where blocklists and user data are stored.
    /// </summary>
    public string RootDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file name used for the cached remote blocklist.
    /// </summary>
    public string RemoteFileName { get; set; } = "disposable_email_blocklist.conf";

    /// <summary>
    /// Gets or sets the file name for the default user-editable blocklist.
    /// </summary>
    public string CustomFileName { get; set; } = "custom_disposable_domains.conf";

    /// <summary>
    /// Gets or sets the URI string from which the disposable domain blocklist is downloaded.
    /// </summary>
    public string RemoteListUri { get; set; } =
        "https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/main/disposable_email_blocklist.conf";

    /// <summary>
    /// Gets or sets the minimum interval between blocklist refresh attempts.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromDays(1);
}