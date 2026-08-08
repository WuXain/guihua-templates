using FreeSql;
using Guihua.Persistence.FreeSql.Domain;

namespace Guihua.Persistence.FreeSql.Infrastructure;

internal sealed class FreeSqlPersistenceRecordStore(IFreeSql freeSql) : IPersistenceRecordStore
{
    public async Task<PersistenceRecord?> FindAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await freeSql.Select<PersistenceRecordEntity>()
            .Where(item => item.Id == id)
            .ToOneAsync(cancellationToken);

        return entity is null ? null : new PersistenceRecord { Id = entity.Id, Value = entity.Value };
    }

    public async Task<PersistenceRecord> SaveAsync(PersistenceRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var entity = new PersistenceRecordEntity { Id = record.Id, Value = record.Value };
        using var connection = await freeSql.Ado.MasterPool.GetAsync(cancellationToken);
        await using var transaction = await connection.Value.BeginTransactionAsync(cancellationToken);

        try
        {
            if (entity.Id == 0)
            {
                entity.Id = await freeSql.Insert(entity)
                    .WithTransaction(transaction)
                    .ExecuteIdentityAsync(cancellationToken);
            }
            else
            {
                var affectedRows = await freeSql.Update<PersistenceRecordEntity>()
                    .SetSource(entity)
                    .Where(item => item.Id == entity.Id)
                    .WithTransaction(transaction)
                    .ExecuteAffrowsAsync(cancellationToken);

                if (affectedRows == 0)
                {
                    throw new InvalidOperationException($"Persistence record {entity.Id} does not exist.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        record.Id = entity.Id;
        return record;
    }
}
