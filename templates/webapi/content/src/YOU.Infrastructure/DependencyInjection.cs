using Microsoft.Extensions.DependencyInjection;
using YOU.Core.Features.RuntimeStatus;

namespace YOU.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The baseline composition root intentionally has no database,
        // cache, network, or device connection to establish.
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<GetRuntimeStatusUseCase>();

        return services;
    }
}
