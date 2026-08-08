using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guihua.HostedService;

internal sealed class HostedServiceWorker<TAdapter>(
    TAdapter adapter,
    IOptions<HostedServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<HostedServiceWorker<TAdapter>> logger) : BackgroundService
    where TAdapter : IHostedServiceAdapter
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        logger.LogInformation(
            "Hosted service started with interval {IntervalMilliseconds} ms and failure policy {FailurePolicy}",
            settings.IntervalMilliseconds,
            settings.FailurePolicy);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await adapter.ExecuteCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (settings.FailurePolicy == HostedServiceFailurePolicy.Stop)
                    {
                        logger.LogError(exception, "Hosted service cycle failed; stopping the hosted service loop");
                        break;
                    }

                    logger.LogError(exception, "Hosted service cycle failed; continuing with the next cycle");
                }

                try
                {
                    await Task.Delay(settings.Interval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            logger.LogInformation("Hosted service stopped");
        }
    }
}
