using Guihua.Persistence.FreeSql;
using Guihua.Persistence.FreeSql.Domain;
using Guihua.Persistence.FreeSql.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Guihua.Persistence.FreeSql.Tests;

public sealed class FreeSqlPersistenceIntegrationTests
{
    [Fact]
    public async Task Sqlite_save_preserves_identity_and_commits_transaction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"guihua-freesql-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FreeSqlPersistence:Provider"] = "Sqlite",
                ["FreeSqlPersistence:ConnectionString"] = $"Data Source={path}",
                ["FreeSqlPersistence:AutoSyncStructure"] = "true"
            }).Build();
            var services = new ServiceCollection().AddLogging();
            services.AddFreeSqlPersistence(configuration);

            await using var provider = services.BuildServiceProvider();
            var store = provider.GetRequiredService<IPersistenceRecordStore>();
            var created = await store.SaveAsync(new PersistenceRecord { Value = "first" });
            var updated = await store.SaveAsync(new PersistenceRecord { Id = created.Id, Value = "second" });
            var loaded = await store.FindAsync(created.Id);

            Assert.NotEqual(0, created.Id);
            Assert.Equal(created.Id, updated.Id);
            Assert.Equal("second", loaded?.Value);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.SaveAsync(new PersistenceRecord { Id = long.MaxValue, Value = "missing" }));
            Assert.Null(await store.FindAsync(long.MaxValue));
        }
        finally
        {
            DeleteSqliteFiles(path);
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
}
