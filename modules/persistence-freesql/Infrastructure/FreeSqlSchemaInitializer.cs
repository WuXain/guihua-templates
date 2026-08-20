using FreeSql;
using Guihua.Persistence.FreeSql.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guihua.Persistence.FreeSql.Infrastructure;

internal sealed class FreeSqlSchemaInitializer(
    IServiceProvider serviceProvider,
    IOptions<FreeSqlPersistenceOptions> options,
    ILogger<FreeSqlSchemaInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.AutoSyncStructure)
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = serviceProvider.GetRequiredService<IFreeSql>();
        logger.LogInformation("FreeSql database schema initialization completed.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
