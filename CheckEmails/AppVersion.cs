namespace CheckEmails;

/// <summary>
/// Provides application version information and related helpers.
/// </summary>
internal static class AppVersion
{
    private const string Version = "1.0.1";

    public static string GetBanner() => $"CheckEmails version {Version}\n";
}