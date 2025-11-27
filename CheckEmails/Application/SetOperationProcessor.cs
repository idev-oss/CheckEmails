using System.Collections.Concurrent;
using System.Net.Mail;
using CheckEmails.CommandLine;
using CheckEmails.Configuration;
using CheckEmails.IO;
using Microsoft.Extensions.Logging;

namespace CheckEmails.Application;

/// <summary>
/// Executes format-only set operations (subtract/merge) over one or more email lists.
/// </summary>
public sealed class SetOperationProcessor
{
    private readonly ILogger<SetOperationProcessor> _logger;

    public SetOperationProcessor(ILogger<SetOperationProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<int> RunAsync(CommandLineOptions options)
    {
        if (options.IncludePaths.Count == 0)
        {
            Console.Error.WriteLine("Set mode requires at least one --include file.");
            return ExitCodes.Error;
        }

        var resultPath = options.ResultPath ?? Path.Combine(Environment.CurrentDirectory, "result_emails.csv");
        if (!EnsureDirectory(resultPath))
        {
            return ExitCodes.Error;
        }

        var includeSet = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var includeTasks = options.IncludePaths.Select(path => LoadFileAsync(path, includeSet));
        var includeSummaries = await Task.WhenAll(includeTasks).ConfigureAwait(false);

        IReadOnlyCollection<FileSummary> excludeSummaries = Array.Empty<FileSummary>();
        if (options.Mode == CommandLineOptions.SetOperationMode.Subtract)
        {
            if (options.ExcludePaths.Count == 0)
            {
                Console.Error.WriteLine("Subtract mode requires at least one --exclude file.");
                return ExitCodes.Error;
            }

            var excludeSet = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var excludeTasks = options.ExcludePaths.Select(path => LoadFileAsync(path, excludeSet));
            excludeSummaries = await Task.WhenAll(excludeTasks).ConfigureAwait(false);

            foreach (var email in excludeSet.Keys)
            {
                includeSet.TryRemove(email, out _);
            }
        }

        if (!await WriteResultAsync(resultPath, includeSet.Keys).ConfigureAwait(false))
        {
            return ExitCodes.Error;
        }

        PrintSummary(options, includeSummaries, excludeSummaries, includeSet.Count, resultPath);
        return ExitCodes.Success;
    }

    private static async Task<FileSummary> LoadFileAsync(string path, ConcurrentDictionary<string, byte> accumulator)
    {
        var summary = new FileSummary(path);

        if (!File.Exists(path))
        {
            summary.Errors.Add("File not found.");
            return summary;
        }

        await foreach (var email in EmailFileReader.ReadAsync(path).ConfigureAwait(false))
        {
            summary.Entries++;

            if (!IsFormatValid(email))
            {
                summary.Invalid++;
                continue;
            }

            summary.Valid++;

            if (accumulator.TryAdd(email, 0))
            {
                summary.Added++;
            }
            else
            {
                summary.Duplicates++;
            }
        }

        return summary;
    }

    private static bool IsFormatValid(string email)
    {
        return MailAddress.TryCreate(email, out _);
    }

    private static async Task<bool> WriteResultAsync(string path, IEnumerable<string> emails)
    {
        try
        {
            await File.WriteAllLinesAsync(path, emails).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to write results to '{path}'. {ex.Message}");
            return false;
        }
    }

    private void PrintSummary(
        CommandLineOptions options,
        IReadOnlyCollection<FileSummary> includeSummaries,
        IReadOnlyCollection<FileSummary> excludeSummaries,
        int totalResult,
        string resultPath)
    {
        Console.WriteLine("Set-mode summary");
        Console.WriteLine($" Mode: {options.Mode}");
        Console.WriteLine($" Output: {resultPath}");

        foreach (var summary in includeSummaries)
        {
            WriteFileSummary("INCLUDE", summary);
        }

        foreach (var summary in excludeSummaries)
        {
            WriteFileSummary("EXCLUDE", summary);
        }

        if (excludeSummaries.Any())
        {
            var excludedCount = excludeSummaries.Sum(s => s.Added);
            Console.WriteLine($" Total excluded (unique): {excludedCount}");
        }

        Console.WriteLine($" Result count (unique): {totalResult}");
    }

    private void WriteFileSummary(string label, FileSummary summary)
    {
        Console.WriteLine($" {label}: {summary.Path}");
        if (summary.Errors.Any())
        {
            foreach (var error in summary.Errors)
            {
                Console.WriteLine($"   ERROR: {error}");
                _logger.LogWarning("Set-mode file issue {File}: {Message}", summary.Path, error);
            }

            return;
        }

        Console.WriteLine($"   Entries: {summary.Entries}");
        Console.WriteLine($"   Valid: {summary.Valid}");
        Console.WriteLine($"   Invalid format: {summary.Invalid}");
        Console.WriteLine($"   Duplicates skipped: {summary.Duplicates}");
        Console.WriteLine($"   Added to set: {summary.Added}");
    }

    private static bool EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(directory);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to create directory '{directory}'. {ex.Message}");
            return false;
        }
    }

    private sealed class FileSummary(string path)
    {
        public string Path { get; } = path;
        public int Entries { get; set; }
        public int Valid { get; set; }
        public int Invalid { get; set; }
        public int Added { get; set; }
        public int Duplicates { get; set; }
        public List<string> Errors { get; } = new();
    }
}