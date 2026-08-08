using FreeSql.DataAnnotations;

namespace Guihua.Persistence.FreeSql.Infrastructure;

[Table(Name = "persistence_records")]
internal sealed class PersistenceRecordEntity
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column(StringLength = 256, IsNullable = false)]
    public required string Value { get; set; }
}
