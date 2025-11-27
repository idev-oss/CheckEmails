namespace CheckEmails.CommandLine;

/// <summary>
/// Represents parsed command-line options for the email validation application.
/// </summary>
public sealed class CommandLineOptions
{
    public enum SetOperationMode
    {
        None,
        Subtract,
        Merge
    }

    /// <summary>
    /// Gets or sets the path to the input file containing email addresses.
    /// </summary>
    public string? InputPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where batch validation results will be written.
    /// </summary>
    public string? ResultsDirectory { get; set; }

    /// <summary>
    /// Gets or sets the path to an additional file with disposable domains supplied by the user.
    /// </summary>
    public string? DisposableDomainsPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the remote disposable-domain list should be refreshed immediately.
    /// </summary>
    public bool RefreshDisposableList { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable debug logging (verbose output with stack traces).
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use UTC time for logs and outputs.
    /// </summary>
    public bool UseUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the application should display help and exit.
    /// </summary>
    public bool ShowHelp { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the application should display version information and exit.
    /// </summary>
    public bool ShowVersion { get; set; }

    /// <summary>
    /// Gets or sets a single email address to validate.
    /// When provided, the application validates only this email and prints the result.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the error message produced during parsing, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the collection of input files to include in set operations.
    /// </summary>
    public IList<string> IncludePaths { get; } = new List<string>();

    /// <summary>
    /// Gets the collection of files containing addresses to exclude in subtract mode.
    /// </summary>
    public IList<string> ExcludePaths { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the output file for set-mode operations.
    /// </summary>
    public string? ResultPath { get; set; }

    /// <summary>
    /// Gets or sets the selected set-operation mode.
    /// </summary>
    public SetOperationMode Mode { get; set; } = SetOperationMode.None;
}