namespace CheckEmails.Configuration;

/// <summary>
/// Provides centralized, compile-time application settings.
/// All values are embedded directly in the executable for single-file AOT deployment.
/// </summary>
internal static class AppSettings
{
    /// <summary>
    /// Gets the application root directory name (relative to user profile).
    /// </summary>
    public const string AppRootDirectoryName = ".checkemails";

    /// <summary>
    /// Gets the full path to the application root directory.
    /// </summary>
    public static string AppRootDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        AppRootDirectoryName);

    /// <summary>
    /// HTTP client timeout for downloading remote resources.
    /// </summary>
    public static readonly TimeSpan HttpClientTimeout = TimeSpan.FromSeconds(15);
}