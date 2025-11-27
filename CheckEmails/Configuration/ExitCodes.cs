namespace CheckEmails.Configuration;

/// <summary>
/// Defines standard exit codes for the application (Unix/POSIX style).
/// </summary>
internal static class ExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int FileNotFound = 2;
    public const int Cancelled = 130;
}