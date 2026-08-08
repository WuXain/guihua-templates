using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Guihua.Integration.Http;

public static class HttpIntegrationServiceCollectionExtensions
{
    public static IHttpClientBuilder AddHttpIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HttpIntegrationOptions>()
            .Bind(configuration.GetSection(HttpIntegrationOptions.SectionName))
            .ValidateOnStart();

        return AddHttpIntegrationClient(services);
    }

    public static IHttpClientBuilder AddHttpIntegration(
        this IServiceCollection services,
        Action<HttpIntegrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<HttpIntegrationOptions>()
            .Configure(configure)
            .ValidateOnStart();

        return AddHttpIntegrationClient(services);
    }

    private static IHttpClientBuilder AddHttpIntegrationClient(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<HttpIntegrationOptions>, HttpIntegrationOptionsValidator>();
        services.AddTransient<OptionalResilienceHandler>();

        return services.AddHttpClient<IExternalStatusClient, ExternalStatusClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<HttpIntegrationOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
            })
            .AddHttpMessageHandler<OptionalResilienceHandler>();
    }
}
