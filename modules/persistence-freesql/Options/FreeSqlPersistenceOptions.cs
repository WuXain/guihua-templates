namespace Guihua.Persistence.FreeSql.Options;

public sealed class FreeSqlPersistenceOptions
{
    public const string SectionName = "FreeSqlPersistence";

    public string Provider { get; set; } = "Sqlite";

    public string ConnectionString { get; set; } = "Data Source=./app.db";

    public bool AutoSyncStructure { get; set; }
}
