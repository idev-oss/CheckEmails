using CheckEmails.Configuration;

namespace CheckEmails.Logging;

/// <summary>
/// Represents the resolved directory and file path for application log output.
/// </summary>
public sealed class LogFileContext
{
    private LogFileContext(string directoryPath, string filePath)
    {
        DirectoryPath = directoryPath;
        FilePath = filePath;
    }

    /// <summary>
    /// Gets the directory where log files are stored.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Gets the absolute path to the current log file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Creates a context rooted within the user's home directory.
    /// </summary>
    public static LogFileContext CreateDefault(bool useUtc = false)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var root = Path.Combine(home, AppSettings.AppRootDirectoryName, "logs");
        Directory.CreateDirectory(root);

        var timestamp = useUtc
            ? DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ")
            : DateTime.Now.ToString("yyyyMMddTHHmmss");

        var fileName = $"checkemails-{timestamp}.log";
        var path = Path.Combine(root, fileName);
        return new LogFileContext(root, path);
    }
}