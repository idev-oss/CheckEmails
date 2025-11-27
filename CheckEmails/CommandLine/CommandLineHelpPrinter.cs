namespace CheckEmails.CommandLine;

/// <summary>
/// Provides usage instructions for the email validation application.
/// </summary>
public static class CommandLineHelpPrinter
{
    private const string HelpText =
        "Usage:\n" +
        "  CheckEmails [options]\n\n" +
        "General options:\n" +
        "  -h, --help                       Show this help message and exit\n" +
        "  -v, --version                    Show application version and disposable list file timestamp\n" +
        "  --debug                          Enable debug mode (verbose logging with stack traces)\n" +
        "  --utc                            Use UTC time for logs and outputs (default is local time)\n" +
        "  -r, --refresh-disposable         Force update of the disposable-domain cache\n" +
        "  -d, --disposable-domains <path>  Use an additional custom disposable-domain list\n" +
        "  -e, --email <address>            Validate one email without reading files\n\n" +
        "Batch validation (default mode):\n" +
        "  -i, --input <path>               REQUIRED. Source file with email addresses (one per line or CSV)\n" +
        "  -o, --results-dir <path>         Directory for output files (valid/invalid/etc)\n" +
        "      --results-directory <path>   Alias for --results-dir\n" +
        "      --output-dir <path>          Alias for --results-dir\n" +
        "      --output <path>              Alias for --results-dir\n\n" +
        "Set operations (format-only compare/merge):\n" +
        "      --set-mode <subtract|merge>  Switch to set-mode processing\n" +
        "      --include <path>             File to include; repeat for multiple inputs\n" +
        "      --exclude <path>             File to exclude (only for subtract mode)\n" +
        "      --result <path>              Output file for the resulting set\n\n" +
        "Notes:\n" +
        "  Custom disposable list default: ~/.checkemails/custom_disposable_domains.conf\n" +
        "  If --results-dir is omitted, output is created under ./checkemails-results-<timestamp>.\n" +
        "  Batch mode writes valid_emails.csv, invalid_emails.csv, invalid_emails_disposable.csv,\n" +
        "  invalid_emails_missing_mx.csv, and info.txt inside the selected directory.\n\n" +
        "Examples:\n" +
        "  CheckEmails --email alice@example.com\n" +
        "  CheckEmails -i data/emails.csv -o out/run-01 -d domains.txt -r\n" +
        "  CheckEmails --set-mode subtract --include master.csv --include new.csv --exclude bounced.csv --result cleaned.csv";

    /// <summary>
    /// Writes the command-line usage information to the standard output.
    /// </summary>
    public static void Print()
    {
        Console.WriteLine(HelpText);
    }
}