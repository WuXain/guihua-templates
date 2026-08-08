using Microsoft.Extensions.Logging;

namespace Guihua.HostedService;

/// <summary>
/// A harmless adapter for verifying module wiring. Replace it with an
/// application adapter before connecting the module to real work.
/// </summary>
public sealed class LoggingHostedServiceAdapter(
    ILogger<LoggingHostedServiceAdapter> logger,
    TimeProvider timeProvider) : IHostedServiceAdapter
{
    public ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Hosted service sample cycle executed at {UtcNow}", timeProvider.GetUtcNow());
        return ValueTask.CompletedTask;
    }
}
