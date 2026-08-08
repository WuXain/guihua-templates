namespace Guihua.HostedService;

/// <summary>
/// Executes one unit of work for the hosted-service loop.
/// Implement this interface in the consuming application for device, protocol,
/// or other application-specific work. The module deliberately contains no
/// UDP, DDS, or other transport implementation.
/// </summary>
public interface IHostedServiceAdapter
{
    ValueTask ExecuteCycleAsync(CancellationToken cancellationToken);
}
