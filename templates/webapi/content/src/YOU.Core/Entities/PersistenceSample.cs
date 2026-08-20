using FreeSql;
using FreeSql.DataAnnotations;

namespace YOU.Core.Entities;

/// <summary>
/// FreeSql persistence sample. Replace or remove it when adding business entities.
/// </summary>
[Table(Name = "persistence_samples")]
public sealed class PersistenceSample : BaseEntity<PersistenceSample, long>
{
    /// <summary>
    /// A user-defined value that demonstrates a required mapped column.
    /// </summary>
    [Column(StringLength = 256, IsNullable = false)]
    public required string Name { get; set; }
}
