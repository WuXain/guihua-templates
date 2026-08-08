using Guihua.Persistence.FreeSql.Domain;

namespace Guihua.Persistence.FreeSql.Infrastructure;

public interface IPersistenceRecordStore
{
    Task<PersistenceRecord> SaveAsync(PersistenceRecord record, CancellationToken cancellationToken = default);

    Task<PersistenceRecord?> FindAsync(long id, CancellationToken cancellationToken = default);
}
