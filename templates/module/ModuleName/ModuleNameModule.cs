using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleName;

public static class ModuleNameModule
{
    public static IServiceCollection AddModuleName(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ModuleNameOptions>()
            .Bind(configuration.GetSection(ModuleNameOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ModuleNameStatus>();
        return services;
    }

    public static IEndpointRouteBuilder MapModuleNameEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/modules/ModuleName/status", (ModuleNameStatus status) =>
            Results.Ok(status.GetStatus()))
            .WithName("ModuleNameStatus");

        return endpoints;
    }
}
