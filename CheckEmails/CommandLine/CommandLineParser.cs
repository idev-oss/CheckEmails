namespace CheckEmails.CommandLine;

/// <summary>Parses command-line arguments for the email validation tool.</summary>
public sealed class CommandLineParser
{
    private static readonly Dictionary<string, Action<CommandLineOptions, string>> ValueHandlers =
        new(StringComparer.Ordinal)
        {
            ["-e"] = static (opts, value) => opts.Email = value,
            ["--email"] = static (opts, value) => opts.Email = value,
            ["-i"] = static (opts, value) => opts.InputPath = value,
            ["--input"] = static (opts, value) => opts.InputPath = value,
            ["-o"] = static (opts, value) => opts.ResultsDirectory = value,
            ["--output"] = static (opts, value) => opts.ResultsDirectory = value,
            ["--output-dir"] = static (opts, value) => opts.ResultsDirectory = value,
            ["--results-dir"] = static (opts, value) => opts.ResultsDirectory = value,
            ["--results-directory"] = static (opts, value) => opts.ResultsDirectory = value,
            ["-d"] = static (opts, value) => opts.DisposableDomainsPath = value,
            ["--disposable-domains"] = static (opts, value) => opts.DisposableDomainsPath = value,
            ["--include"] = static (opts, value) => opts.IncludePaths.Add(value),
            ["--exclude"] = static (opts, value) => opts.ExcludePaths.Add(value),
            ["--result"] = static (opts, value) => opts.ResultPath = value,
            ["--set-mode"] = static (opts, value) =>
            {
                if (!TryParseMode(value, out var mode))
                {
                    opts.ErrorMessage = $"Unknown set mode: {value}";
                }
                else
                {
                    opts.Mode = mode;
                }
            }
        };

    private static readonly Dictionary<string, Action<CommandLineOptions>> FlagHandlers =
        new(StringComparer.Ordinal)
        {
            ["-h"] = static opts => opts.ShowHelp = true,
            ["--help"] = static opts => opts.ShowHelp = true,
            ["-v"] = static opts => opts.ShowVersion = true,
            ["--version"] = static opts => opts.ShowVersion = true,
            ["-r"] = static opts => opts.RefreshDisposableList = true,
            ["--refresh-disposable"] = static opts => opts.RefreshDisposableList = true,
            ["--debug"] = static opts => opts.Debug = true,
            ["--utc"] = static opts => opts.UseUtc = true
        };

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        if (args.Length == 0)
        {
            options.ShowHelp = true;
            return options;
        }

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (FlagHandlers.TryGetValue(arg, out var flagHandler))
            {
                flagHandler(options);
                continue;
            }

            if (ValueHandlers.TryGetValue(arg, out var handler))
            {
                if (!TryReadValue(args, ref index, out var value))
                {
                    options.ErrorMessage = $"Missing value for {arg} option.";
                    return options;
                }

                handler(options, value);
                if (!string.IsNullOrEmpty(options.ErrorMessage))
                {
                    return options;
                }

                continue;
            }

            options.ErrorMessage = $"Unknown option: {arg}";
            return options;
        }

        return options;
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseMode(string? value, out CommandLineOptions.SetOperationMode mode)
    {
        switch (value?.ToLowerInvariant())
        {
            case "subtract":
                mode = CommandLineOptions.SetOperationMode.Subtract;
                return true;
            case "merge":
                mode = CommandLineOptions.SetOperationMode.Merge;
                return true;
            default:
                mode = CommandLineOptions.SetOperationMode.None;
                return false;
        }
    }
}