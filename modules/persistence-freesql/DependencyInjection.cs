using FreeSql;
using Guihua.Persistence.FreeSql.Infrastructure;
using Guihua.Persistence.FreeSql.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guihua.Persistence.FreeSql;

public static class DependencyInjection
{
    public static IServiceCollection AddFreeSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

            if (options.AutoSyncStructure)
            {
                logger.LogWarning(
                    "FreeSql AutoSyncStructure is enabled. Schema changes will be applied automatically; disable it for controlled migrations.");
                freeSql.CodeFirst.SyncStructure(typeof(PersistenceRecordEntity));
            }

            return freeSql;
        });

        services.AddScoped<IPersistenceRecordStore, FreeSqlPersistenceRecordStore>();
        return services;
    }
}
