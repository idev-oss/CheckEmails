namespace CheckEmails.Validation;

/// <summary>
/// Defines methods for configuring disposable email domain sources.
/// </summary>
public interface IDisposableEmailSourceConfigurator
{
    /// <summary>
    /// Prepares the checker by creating necessary directories and loading initial domain lists.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Overrides the default path for the user-maintained disposable domain list.
    /// </summary>
    /// <param name="path">An absolute or relative path to the custom domain file.</param>
    Task SetCustomFileAsync(string? path);

    /// <summary>
    /// Forces a refresh of the remote disposable domain list.
    /// </summary>
    /// <param name="forceDownload">If true, downloads the list even if the cache is recent.</param>
    Task RefreshAsync(bool forceDownload);
    
    /// <summary>
    /// Forces a download of the remote disposable domain list, overwriting any cached version.
    /// </summary>
    Task DownloadRemoteListAsync();
}