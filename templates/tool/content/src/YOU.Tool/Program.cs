using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YOU.Tool;

public sealed class ToolOptions
{
    public const string SectionName = "GuihuaTool";
    public int IntervalMilliseconds { get; set; } = 1000;
    public int Iterations { get; set; }
}

public static class Program
{
    public const int SuccessExitCode = 0;
    public const int UnexpectedErrorExitCode = 1;
    public const int ConfigurationExitCode = 2;
    public const int CancelledExitCode = 130;

    public static async Task<int> Main(string[] args)
    {
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging => logging.ClearProviders().AddConsole())
            .ConfigureServices((context, services) => services.AddOptions<ToolOptions>()
                .Bind(context.Configuration.GetSection(ToolOptions.SectionName)))
            .Build();

        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("YOU.Tool");
        ToolOptions options = host.Services.GetRequiredService<IOptions<ToolOptions>>().Value;
        if (options.IntervalMilliseconds < 50 || options.Iterations < 0)
        {
            logger.LogError("Invalid GuihuaTool configuration. IntervalMilliseconds must be at least 50 and Iterations cannot be negative.");
            return ConfigurationExitCode;
        }

        using CancellationTokenSource cancellation = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            logger.LogInformation("Starting the local sample loop. Press Ctrl+C to cancel.");
            await RunLocalSampleAsync(options, cancellation.Token, logger);
            logger.LogInformation("Tool completed successfully.");
            return SuccessExitCode;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            logger.LogInformation("Cancellation requested; exiting normally.");
            return CancelledExitCode;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The tool terminated unexpectedly.");
            return UnexpectedErrorExitCode;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task RunLocalSampleAsync(ToolOptions options, CancellationToken cancellationToken, ILogger logger)
    {
        for (int iteration = 1; options.Iterations == 0 || iteration <= options.Iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Local sample iteration {Iteration}; no device or production endpoint is contacted.", iteration);
            await Task.Delay(options.IntervalMilliseconds, cancellationToken);
        }
    }
}
