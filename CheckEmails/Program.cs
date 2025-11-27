using CheckEmails;
using CheckEmails.Application;
using CheckEmails.CommandLine;
using CheckEmails.Configuration;
using CheckEmails.Logging;
using CheckEmails.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

var options = CommandLineParser.Parse(args);
Console.WriteLine(AppVersion.GetBanner());

if (options.ShowVersion)
{
    PrintDisposableBlocklistInfo(options.UseUtc);
    return ExitCodes.Success;
}

if (options.ShowHelp)
{
    CommandLineHelpPrinter.Print();
    return ExitCodes.Success;
}

// Display cancellation hint to user
Console.WriteLine("Press Ctrl+C at any time to cancel. The current results folder will be deleted on cancellation.\n");

// Create a CancellationTokenSource that will be triggered by Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate process termination
    cts.Cancel();
};

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddSingleton(LogFileContext.CreateDefault(options.UseUtc));
builder.Services.AddSingleton<FileLoggerProvider>();
builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<FileLoggerProvider>());

builder.Services.AddApplicationServices();

using var host = builder.Build();

var app = host.Services.GetRequiredService<EmailValidationApp>();
// Use CancellationToken from Console.CancelKeyPress handler for graceful cancellation
return await app.RunAsync(options, args, cts.Token);

// Prints disposable blocklist file information
static void PrintDisposableBlocklistInfo(bool useUtc)
{
    var checkerOptions = new DisposableEmailCheckerOptions
    {
        RootDirectory = AppSettings.AppRootDirectory
    };

    var remoteFilePath = Path.Combine(checkerOptions.RootDirectory, checkerOptions.RemoteFileName);

    if (!File.Exists(remoteFilePath))
    {
        Console.WriteLine("Disposable blocklist file not found.\n");
        Console.WriteLine(
            $"Expected location: {remoteFilePath}.\n It will be created when the disposable list is downloaded (e.g., with -r/--refresh-disposable).\n");
        return;
    }

    var fileInfo = new FileInfo(remoteFilePath);
    var timestamp = useUtc
        ? fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
        : fileInfo.LastWriteTime.ToString(CultureInfo.CurrentCulture);

    Console.WriteLine($"Disposable blocklist file: {remoteFilePath}");
    Console.WriteLine($"Disposable blocklist file last updated at: {timestamp}\n");
}

namespace CheckEmails
{
    /// <summary>
    /// Extension methods for service registration - AOT compatible (no reflection-based binding).
    /// All settings use compile-time defaults from option classes and AppSettings.
    /// </summary>
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register options with compile-time defaults (AOT compatible - no config file needed)
            services.AddOptions<DisposableEmailCheckerOptions>()
                .Configure(options =>
                {
                    options.RootDirectory = AppSettings.AppRootDirectory;
                    // All other options use their default values from DisposableEmailCheckerOptions
                });

            services.AddOptions<MxRecordCheckerOptions>();
            // MxRecordCheckerOptions uses its default values (no configuration needed)

            // Register HttpClient for DisposableEmailChecker
            services.AddHttpClient<DisposableEmailChecker>(client =>
            {
                client.Timeout = AppSettings.HttpClientTimeout;
            });

            services.AddSingleton<EmailValidationApp>();
            services.AddSingleton<IMxRecordChecker, MxRecordChecker>();

            // Register DisposableEmailChecker and its interfaces
            services.AddSingleton<DisposableEmailChecker>();
            services.AddSingleton<IEmailDisposableChecker>(sp => sp.GetRequiredService<DisposableEmailChecker>());
            services.AddSingleton<IDisposableEmailSourceConfigurator>(sp =>
                sp.GetRequiredService<DisposableEmailChecker>());

            services.AddSingleton<SetOperationProcessor>();
            services.AddSingleton<EmailValidationService>();

            return services;
        }
    }
}