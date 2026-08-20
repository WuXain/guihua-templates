using FreeSql;
using FreeSql.DataAnnotations;

namespace Guihua.Persistence.FreeSql.Infrastructure;

[Table(Name = "persistence_records")]
internal sealed class PersistenceRecordEntity
    : BaseEntity<PersistenceRecordEntity, long>
{
    [Column(StringLength = 256, IsNullable = false)]
    public required string Value { get; set; }
}
