using System.Text;
using System.Threading.Channels;
using CheckEmails.CommandLine;
using CheckEmails.Configuration;
using CheckEmails.IO;
using CheckEmails.Logging;
using CheckEmails.Validation;
using Microsoft.Extensions.Logging;

namespace CheckEmails.Application;

/// <summary>
/// Coordinates command-line parsing and email validation workflow.
/// </summary>
public sealed class EmailValidationApp(
    EmailValidationService validationService,
    ILogger<EmailValidationApp> logger,
    LogFileContext logFileContext,
    IDisposableEmailSourceConfigurator disposableSourceConfigurator,
    SetOperationProcessor setOperationProcessor,
    FileLoggerProvider fileLoggerProvider)
{
    private bool _resultsCleanedUpAfterCancellation;

    public async Task<int> RunAsync(CommandLineOptions options, string[] args,
        CancellationToken cancellationToken = default)
    {
        _resultsCleanedUpAfterCancellation = false;

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            fileLoggerProvider.IsDebugMode = options.Debug;
            fileLoggerProvider.UseUtc = options.UseUtc;
            if (options.Debug)
            {
                logger.LogInformation("Debug mode enabled.");
            }

            logger.LogInformation("Run started. Args: {Args}",
                args is { Length: > 0 } ? string.Join(' ', args) : "<none>");

            if (!string.IsNullOrEmpty(options.ErrorMessage))
            {
                logger.LogWarning("Argument parsing failed: {Error}", options.ErrorMessage);
                await Console.Error.WriteLineAsync(options.ErrorMessage);
                CommandLineHelpPrinter.Print();
                return ExitCodes.Error;
            }

            await disposableSourceConfigurator.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.Mode != CommandLineOptions.SetOperationMode.None)
            {
                return await setOperationProcessor.RunAsync(options).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(options.DisposableDomainsPath))
            {
                await disposableSourceConfigurator.SetCustomFileAsync(options.DisposableDomainsPath)
                    .ConfigureAwait(false);
                logger.LogInformation("Custom disposable domain list configured: {Path}",
                    options.DisposableDomainsPath);
            }

            if (options.RefreshDisposableList)
            {
                if (IsRefreshOnly(options))
                {
                    await disposableSourceConfigurator.DownloadRemoteListAsync().ConfigureAwait(false);
                    logger.LogInformation("Disposable domain list downloaded explicitly.");
                    Console.WriteLine("Disposable domain list has been downloaded.");
                    return ExitCodes.Success;
                }

                await disposableSourceConfigurator.RefreshAsync(forceDownload: true).ConfigureAwait(false);
                logger.LogInformation("Disposable domain list refreshed explicitly.");
                Console.WriteLine("Disposable domain list has been refreshed.");
            }

            if (string.IsNullOrWhiteSpace(options.Email) && string.IsNullOrWhiteSpace(options.InputPath))
            {
                const string inputRequiredMessage = "Input file path is missing. Use -i or --input.";
                logger.LogWarning("Bulk validation aborted: input file path is missing.");
                await Console.Error.WriteLineAsync(inputRequiredMessage);
                return ExitCodes.Error;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(options.Email))
            {
                return await RunSingleEmailValidationAsync(options.Email, cancellationToken).ConfigureAwait(false);
            }

            return await RunBulkEmailValidationAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var message = _resultsCleanedUpAfterCancellation
                ? "Operation canceled by user. Temporary results have been removed."
                : "Operation canceled by user.";
            Console.WriteLine(message);
            logger.LogInformation("Execution canceled by user.");
            return ExitCodes.Cancelled;
        }
    }

    private async Task<int> RunSingleEmailValidationAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            Console.WriteLine("Result: invalid email (empty value)");
            logger.LogInformation("Single-email mode: empty input provided.");
            return ExitCodes.Success;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var validation = await validationService.ValidateAsync(email).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Single-email validation completed. Email={Email} Valid={IsValid} Reason={Reason}",
            email,
            validation.IsValid,
            validation.Reason);

        Console.WriteLine(validation.IsValid
            ? $"Result: valid email ({email})"
            : $"Result: invalid email ({email}) — {DescribeReason(validation.Reason)}");

        Console.WriteLine($"Log saved to: {logFileContext.FilePath}");
        return ExitCodes.Success;
    }

    private async Task<int> RunBulkEmailValidationAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new InvalidOperationException("Input path is required for bulk validation.");
        }

        var paths = ResolvePaths(options);

        try
        {
            if (!File.Exists(paths.Input))
            {
                logger.LogError("Input file not found: {Path}", paths.Input);
                await Console.Error.WriteLineAsync($"Input file not found: {paths.Input}");
                return ExitCodes.FileNotFound;
            }

            EnsureDirectoryExists(paths.ResultsDirectory);

            Console.WriteLine("Starting email validation...");
            logger.LogInformation(
                "Bulk validation started. Input={Input}, ResultsDirectory={Results}, ValidOutput={Valid}, InvalidOutput={Invalid}, DisposableOutput={Disposable}, MissingMxOutput={MissingMx}",
                paths.Input, paths.ResultsDirectory, paths.Valid, paths.Invalid, paths.InvalidDisposable,
                paths.InvalidMissingMx);

            // Count exact number of emails without loading them into memory
            var emailCount = await EmailFileReader.CountAsync(paths.Input, cancellationToken).ConfigureAwait(false);
            if (emailCount == 0)
            {
                logger.LogWarning("Input file is empty or contains no emails. Aborting.");
                Console.WriteLine("Input file is empty. Nothing to do.");
                return ExitCodes.Success;
            }

            Console.WriteLine($"Found {emailCount} email entries to validate.");
            logger.LogInformation("Found {Count} emails to process (exact count).", emailCount);

            var progress = new ValidationProgress(emailCount);
            using var progressBar = new ConsoleProgressBar(progress);
            progressBar.Refresh(force: true);
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var progressTask = RunProgressLoopAsync(progressBar, progressCts.Token);

            // Use bounded channels to control memory consumption
            // Reduced buffer size to minimize memory usage while maintaining throughput
            var channelOptions = new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true, // Optimization: single reader per channel
                SingleWriter = false // Multiple writers from parallel processing
            };
            var validChannel = Channel.CreateBounded<string>(channelOptions);
            var invalidChannel = Channel.CreateBounded<string>(channelOptions);
            var disposableChannel = Channel.CreateBounded<string>(channelOptions);
            var missingMxChannel = Channel.CreateBounded<string>(channelOptions);

            var writerTasks = new[]
            {
                WriteResultsToFileAsync(validChannel.Reader, paths.Valid, cancellationToken),
                WriteResultsToFileAsync(invalidChannel.Reader, paths.Invalid, cancellationToken),
                WriteResultsToFileAsync(disposableChannel.Reader, paths.InvalidDisposable, cancellationToken),
                WriteResultsToFileAsync(missingMxChannel.Reader, paths.InvalidMissingMx, cancellationToken)
            };

            var emails = EmailFileReader.ReadAsync(paths.Input, cancellationToken);
            var logicalCores = Environment.ProcessorCount;
            var parallelism = Math.Max(1, logicalCores / 2);
            logger.LogInformation("Using parallelism: {Parallelism} (logical cores: {Cores})", parallelism,
                logicalCores);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism,
                CancellationToken = cancellationToken
            };

            try
            {
                await Parallel.ForEachAsync(emails, parallelOptions, async (email, loopToken) =>
                {
                    loopToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        progress.RecordResult(EmailValidationResult.Invalid(EmailRejectionReason.InvalidFormat));
                        return;
                    }

                    var validation = await validationService.ValidateAsync(email);

                    var writer = validation.Reason switch
                    {
                        _ when validation.IsValid => validChannel.Writer,
                        EmailRejectionReason.DisposableDomain => disposableChannel.Writer,
                        EmailRejectionReason.MissingMxRecords => missingMxChannel.Writer,
                        _ => invalidChannel.Writer
                    };
                    await writer.WriteAsync(email, loopToken);

                    progress.RecordResult(validation);
                }).ConfigureAwait(false);
            }
            finally
            {
                // Stop timing immediately to freeze the throughput calculation
                progress.StopTiming();
                await progressCts.CancelAsync();
                await progressTask.ConfigureAwait(false);
                progressBar.Complete();
            }

            Console.WriteLine("Validation finished. Writing results to disk, please wait...");
            logger.LogInformation("Validation processing complete. Flushing results to disk.");

            validChannel.Writer.Complete();
            invalidChannel.Writer.Complete();
            disposableChannel.Writer.Complete();
            missingMxChannel.Writer.Complete();

            await Task.WhenAll(writerTasks).ConfigureAwait(false);

            Console.WriteLine("Finished writing results.");
            logger.LogInformation("All result writers have finished.");


            cancellationToken.ThrowIfCancellationRequested();

            var finalSnapshot = progress.GetSnapshot();
            var finishedTime = options.UseUtc ? DateTime.UtcNow : DateTime.Now;
            await WriteSummaryAsync(paths, finalSnapshot, finishedTime, logFileContext.FilePath).ConfigureAwait(false);

            Console.WriteLine("\nValidation completed.");
            Console.WriteLine($"  Processed: {finalSnapshot.Processed}");
            Console.WriteLine($"    Valid: {finalSnapshot.Valid}");
            Console.WriteLine(
                $"    Invalid: {finalSnapshot.Invalid} (disposable: {finalSnapshot.InvalidDisposable}, missing MX: {finalSnapshot.InvalidMissingMx}, format: {finalSnapshot.InvalidFormat})");
            Console.WriteLine($"Results directory: {paths.ResultsDirectory}");
            Console.WriteLine($"  Summary: {paths.SummaryFile}");
            Console.WriteLine($"Log saved to: {logFileContext.FilePath}");

            logger.LogInformation(
                "Bulk validation completed. Total={Total}, Valid={Valid}, Invalid={Invalid}, Disposable={Disposable}, MissingMx={MissingMx}, InvalidFormat={InvalidFormat}",
                finalSnapshot.Processed, finalSnapshot.Valid, finalSnapshot.Invalid, finalSnapshot.InvalidDisposable,
                finalSnapshot.InvalidMissingMx, finalSnapshot.InvalidFormat);
            logger.LogInformation("Log file located at {LogFile}", logFileContext.FilePath);

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            _resultsCleanedUpAfterCancellation = TryCleanupResults(paths);
            throw;
        }
    }

    private static async Task WriteResultsToFileAsync(ChannelReader<string> reader, string filePath,
        CancellationToken cancellationToken)
    {
        await using var writer = EmailResultWriter.Create(filePath);
        // Use ReadAllAsync for efficient channel reading
        await foreach (var email in reader.ReadAllAsync(cancellationToken))
        {
            await writer.WriteAsync(email);
        }
    }

    private static async Task WriteSummaryAsync(
        ResolvedPaths paths,
        ValidationProgressSnapshot snapshot,
        DateTime finishedTime,
        string logFilePath)
    {
        var lines = new[]
        {
            $"Run started: {paths.StartedTime:O}",
            $"Run finished: {finishedTime:O}",
            $"Duration: {(finishedTime - paths.StartedTime):hh\\:mm\\:ss}",
            $"Input file: {paths.Input}",
            $"Results directory: {paths.ResultsDirectory}",
            $"Valid output: {paths.Valid}",
            $"Invalid output: {paths.Invalid}",
            $"Disposable output: {paths.InvalidDisposable}",
            $"Missing MX output: {paths.InvalidMissingMx}",
            $"Total processed: {snapshot.Processed}",
            $"Valid: {snapshot.Valid}",
            $"Invalid total: {snapshot.Invalid}",
            $"  Disposable domains: {snapshot.InvalidDisposable}",
            $"  Missing MX: {snapshot.InvalidMissingMx}",
            $"  Invalid format: {snapshot.InvalidFormat}",
            $"Average throughput: {snapshot.Throughput:0.00} emails/sec",
            $"Log file: {logFilePath}"
        };

        await File.WriteAllLinesAsync(paths.SummaryFile, lines, Encoding.UTF8).ConfigureAwait(false);
    }

    private static Task RunProgressLoopAsync(ConsoleProgressBar progressBar, CancellationToken token,
        int refreshIntervalMs = 125)
    {
        return Task.Run(async () =>
        {
            var interval = TimeSpan.FromMilliseconds(refreshIntervalMs);
            while (!token.IsCancellationRequested)
            {
                progressBar.Refresh();
                try
                {
                    await Task.Delay(interval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });
    }

    private bool TryCleanupResults(ResolvedPaths paths)
    {
        var directory = paths.ResultsDirectory;
        if (string.IsNullOrWhiteSpace(directory)) return false;

        try
        {
            if (paths.DirectoryPreexisted)
            {
                var removedAny = DeleteFileIfExists(paths.Valid);
                removedAny |= DeleteFileIfExists(paths.Invalid);
                removedAny |= DeleteFileIfExists(paths.InvalidDisposable);
                removedAny |= DeleteFileIfExists(paths.InvalidMissingMx);
                removedAny |= DeleteFileIfExists(paths.SummaryFile);
                if (removedAny)
                    logger.LogInformation("Temporary result files removed after cancellation: {Directory}", directory);
                return removedAny;
            }

            if (!Directory.Exists(directory)) return false;
            Directory.Delete(directory, recursive: true);
            logger.LogInformation("Temporary results removed after cancellation: {Directory}", directory);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove temporary results after cancellation: {Directory}", directory);
            return false;
        }

        static bool DeleteFileIfExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
    }

    private static ResolvedPaths ResolvePaths(CommandLineOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new InvalidOperationException("Input path is required to resolve batch run paths.");
        }

        var input = Path.GetFullPath(options.InputPath);
        var startedTime = options.UseUtc ? DateTime.UtcNow : DateTime.Now;
        var currentDirectory = Environment.CurrentDirectory;
        var targetDirectory = string.IsNullOrWhiteSpace(options.ResultsDirectory)
            ? Path.GetFullPath(Path.Combine(currentDirectory, $"checkemails-results-{startedTime:yyyyMMdd-HHmmss}"))
            : Path.GetFullPath(options.ResultsDirectory);
        var directoryPreexisted = Directory.Exists(targetDirectory);

        return new ResolvedPaths(
            input,
            targetDirectory,
            Path.Combine(targetDirectory, "valid_emails.csv"),
            Path.Combine(targetDirectory, "invalid_emails_format.csv"),
            Path.Combine(targetDirectory, "invalid_emails_disposable.csv"),
            Path.Combine(targetDirectory, "invalid_emails_missing_mx.csv"),
            Path.Combine(targetDirectory, "info.txt"),
            startedTime,
            directoryPreexisted);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private sealed record ResolvedPaths(
        string Input,
        string ResultsDirectory,
        string Valid,
        string Invalid,
        string InvalidDisposable,
        string InvalidMissingMx,
        string SummaryFile,
        DateTime StartedTime,
        bool DirectoryPreexisted);

    private static string DescribeReason(EmailRejectionReason reason) =>
        reason switch
        {
            EmailRejectionReason.InvalidFormat => "invalid format",
            EmailRejectionReason.DisposableDomain => "disposable domain",
            EmailRejectionReason.MissingMxRecords => "domain has no MX records",
            _ => "unknown reason"
        };

    private static bool IsRefreshOnly(CommandLineOptions options)
    {
        return options.RefreshDisposableList &&
               string.IsNullOrWhiteSpace(options.Email) &&
               string.IsNullOrWhiteSpace(options.InputPath) &&
               string.IsNullOrWhiteSpace(options.ResultsDirectory) &&
               string.IsNullOrWhiteSpace(options.DisposableDomainsPath) &&
               options.Mode == CommandLineOptions.SetOperationMode.None &&
               string.IsNullOrWhiteSpace(options.ResultPath) &&
               options.IncludePaths.Count == 0 &&
               options.ExcludePaths.Count == 0;
    }
}