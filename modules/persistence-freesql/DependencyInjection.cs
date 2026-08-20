using System.Reflection;
using FreeSql;
using Guihua.Persistence.FreeSql.Infrastructure;
using Guihua.Persistence.FreeSql.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guihua.Persistence.FreeSql;

public static class DependencyInjection
{
    public static IServiceCollection AddFreeSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] entityAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var entityTypes = GetEntityTypes(entityAssemblies);

        services.AddOptions<FreeSqlPersistenceOptions>()
            .Bind(configuration.GetSection(FreeSqlPersistenceOptions.SectionName))
            .Validate(options => string.Equals(options.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(options.Provider, "MySql", StringComparison.OrdinalIgnoreCase),
                "Provider must be Sqlite or MySql.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ConnectionString is required.")
            .ValidateOnStart();

        services.AddSingleton<IFreeSql>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<FreeSqlPersistenceOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FreeSqlPersistence");
            var dataType = string.Equals(options.Provider, "MySql", StringComparison.OrdinalIgnoreCase)
                ? DataType.MySql
                : DataType.Sqlite;

            var freeSql = new FreeSqlBuilder()
                .UseConnectionString(dataType, options.ConnectionString)
                .UseAutoSyncStructure(false)
                .Build();

            BaseEntity.Initialization(freeSql, null);

            if (options.AutoSyncStructure)
            {
                logger.LogWarning(
                    "FreeSql AutoSyncStructure is enabled. Synchronizing {EntityCount} explicitly registered entity types; disable it for controlled migrations.",
                    entityTypes.Length);
                freeSql.CodeFirst.SyncStructure(entityTypes);
            }

            return freeSql;
        });

        services.AddScoped<IPersistenceRecordStore, FreeSqlPersistenceRecordStore>();
        services.AddHostedService<FreeSqlSchemaInitializer>();
        return services;
    }

    private static Type[] GetEntityTypes(IEnumerable<Assembly>? entityAssemblies)
    {
        var entityTypes = new List<Type> { typeof(PersistenceRecordEntity) };

        foreach (var assembly in entityAssemblies ?? [])
        {
            ArgumentNullException.ThrowIfNull(assembly);
            entityTypes.AddRange(GetLoadableTypes(assembly).Where(IsBaseEntityType));
        }

        return entityTypes.Distinct().ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private static bool IsBaseEntityType(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(BaseEntity<,>))
            {
                return true;
            }
        }

        return false;
    }
}
