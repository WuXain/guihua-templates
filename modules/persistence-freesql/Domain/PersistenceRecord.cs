namespace Guihua.Persistence.FreeSql.Domain;

public sealed class PersistenceRecord
{
    public long Id { get; set; }

    public required string Value { get; set; }
}
