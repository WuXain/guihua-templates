using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Guihua.HostedService;

public static class HostedServiceRegistration
{
    public static IServiceCollection AddGuihuaHostedService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddGuihuaHostedService<LoggingHostedServiceAdapter>(configuration);
    }

    public static IServiceCollection AddGuihuaHostedService<TAdapter>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TAdapter : class, IHostedServiceAdapter
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HostedServiceOptions>()
            .Bind(configuration.GetSection(HostedServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => Enum.IsDefined(options.FailurePolicy),
                $"{HostedServiceOptions.SectionName}:FailurePolicy must be Continue or Stop.")
            .ValidateOnStart();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<TAdapter>();
        services.AddHostedService<HostedServiceWorker<TAdapter>>();

        return services;
    }
}
