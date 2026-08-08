using Guihua.HostedService;
using Guihua.Integration.Http;
using Guihua.Persistence.FreeSql;
using Guihua.Persistence.FreeSql.Domain;
using Guihua.Persistence.FreeSql.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Guihua.ModuleIntegrationTests;

public sealed class ModuleCompositionTests
{
    [Fact]
    public async Task Common_module_combination_starts_and_runs_without_external_services()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"guihua-composition-{Guid.NewGuid():N}.db");
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FreeSqlPersistence:Provider"] = "Sqlite",
                ["FreeSqlPersistence:ConnectionString"] = $"Data Source={databasePath}",
                ["FreeSqlPersistence:AutoSyncStructure"] = "true",
                ["GuihuaHostedService:IntervalMilliseconds"] = "10",
                ["GuihuaHostedService:FailurePolicy"] = nameof(HostedServiceFailurePolicy.Continue),
                ["IntegrationHttp:BaseAddress"] = "http://127.0.0.1/",
                ["IntegrationHttp:Timeout"] = "00:00:02",
                ["IntegrationHttp:EnableResilience"] = "false"
            });

            builder.Services.AddFreeSqlPersistence(builder.Configuration);
            builder.Services.AddGuihuaHostedService<CompositionAdapter>(builder.Configuration);
            builder.Services.AddHttpIntegration(builder.Configuration);

            using var host = builder.Build();
            await host.StartAsync();

            var adapter = host.Services.GetRequiredService<CompositionAdapter>();
            await adapter.Executed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var store = host.Services.GetRequiredService<IPersistenceRecordStore>();
            var saved = await store.SaveAsync(new PersistenceRecord { Value = "combined" });
            var loaded = await store.FindAsync(saved.Id);

            Assert.Equal("combined", loaded?.Value);
            Assert.NotNull(host.Services.GetRequiredService<IExternalStatusClient>());

            await host.StopAsync();
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static void DeleteSqliteFiles(string path)
    {
        foreach (var candidate in new[] { path, $"{path}-wal", $"{path}-shm" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }

    private sealed class CompositionAdapter : IHostedServiceAdapter
    {
        public TaskCompletionSource<bool> Executed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Executed.TrySetResult(true);
            return ValueTask.CompletedTask;
        }
    }
}
