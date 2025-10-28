using ClearFlow.StatePersistence.Aggregates;
using ClearFlow.StatePersistence.StateStores;
using EventFlow.Core;
using EventFlow.EntityFramework;
using EventFlow.EntityFramework.Extensions;
using EventFlow.EventStores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClearFlow.StatePersistence.EntityFramework;

public abstract class StatePersistenceBase<TDbContext, TStateTable, TStateModel>
        where TDbContext : DbContext
        where TStateTable : class, IStateTable
        where TStateModel : class, IStateModel, new()
{
    protected readonly IDbContextProvider<TDbContext> contextProvider;
    private readonly IUniqueConstraintDetectionStrategy _strategy;
    public Type StateType => typeof(TStateModel);

    protected StatePersistenceBase(IDbContextProvider<TDbContext> contextProvider, IUniqueConstraintDetectionStrategy strategy)
    {
        this.contextProvider = contextProvider;
        _strategy = strategy;
    }

    public async Task<TStateModel> GetAsync(IIdentity id, CancellationToken cancellationToken)
    {
        return await GetAsync(id.Value, cancellationToken);
    }

    public async Task<TStateModel> GetAsync(string id, CancellationToken cancellationToken)
    {
        var table = await GetTableAsync(id, cancellationToken);
        return ToDto(table);
    }

    public async Task<TStateTable> GetTableAsync(string id, CancellationToken cancellationToken)
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            var model = await dbContext.Set<TStateTable>()
                .AsNoTracking()
                .FirstAsync(s => s.Id == id, cancellationToken);

            return model;
        }
    }

    public async Task<TStateModel> TryGetAsync(string id, CancellationToken cancellationToken)
    {
        var table = await TryGetTableAsync(id, cancellationToken);
        return table != null ? ToDto(table) : null;
    }

    public async Task<TStateTable> TryGetTableAsync(string id, CancellationToken cancellationToken)
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            var model = await dbContext.Set<TStateTable>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            return model;
        }
    }

    public async Task<TStateModel> GetOrCreateAsync(string id, CancellationToken cancellationToken)
    {
        var existingDto = await TryGetAsync(id, cancellationToken);
        return existingDto ?? new TStateModel();
    }

    public async Task<TrackedModel<TStateModel>> GetOrCreateAsync(IIdentity id, CancellationToken cancellationToken)
    {
        var existingTable = await TryGetTableAsync(id.Value, cancellationToken);

        return existingTable != null ? TrackedModel<TStateModel>.From(ToDto(existingTable), existingTable.Version) : TrackedModel<TStateModel>.FromEmptyState(new TStateModel());
    }

    public async Task InsertAsync(string id, TStateModel model, long version, CancellationToken cancellationToken)
    {
        var table = ToTable(id, model, version);
        await InsertTableAsync(table, cancellationToken);
    }

    public async Task InsertTableAsync(TStateTable model, CancellationToken cancellationToken)
    {
        await InsertTableAsync<TStateTable>(model, cancellationToken);
    }

    public async Task InsertTableAsync<TTargetTable>(TTargetTable model, CancellationToken cancellationToken)
        where TTargetTable : class, IStateTable
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            dbContext.Add(model);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation(_strategy))
            {
                // If we have a duplicate key exception, then the model has already been created
            }
        }
    }

    public async Task UpdateAsync(string id, TStateModel model, long version, CancellationToken cancellationToken)
    {
        var table = ToTable(id, model, version);
        await UpdateTableAsync(table, cancellationToken);
    }

    public async Task UpdateTableAsync(TStateTable model, CancellationToken cancellationToken)
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            model.Version++;
            dbContext.Entry(model).State = EntityState.Modified;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpsertAsync(IIdentity id, TrackedModel<TStateModel> model, CancellationToken cancellationToken)
    {
        if (model.IsDeleted)
        {
            await DeleteAsync(id, cancellationToken);
            return;
        }

        await UpsertAsync(id.Value, model.Model, model.Version, cancellationToken);
    }

    public async Task UpsertAsync(string id, TStateModel model, long version, CancellationToken cancellationToken)
    {
        var table = ToTable(id, model, version);
        await UpsertTableAsync(table, cancellationToken);
    }

    public async Task UpsertTableAsync(TStateTable model, CancellationToken cancellationToken)
    {
        await UpsertStateTableAsync(model, cancellationToken);
    }

    public async Task<bool> UpsertStateTableAsync<TTargetTable>(TTargetTable model, CancellationToken cancellationToken)
        where TTargetTable : class, IStateTable
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            var existingModel = await dbContext.Set<TTargetTable>()
                .FirstOrDefaultAsync(s => s.Id == model.Id, cancellationToken);

            if (existingModel != null)
            {
                model.Version++;

                dbContext.Entry(existingModel).CurrentValues.SetValues(model);
                dbContext.Update(existingModel).Property(x => x.GlobalSequenceNumber).IsModified = false;
            }
            else
            {
                dbContext.Add(model);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return existingModel != null;
        }
    }

    public async Task UpsertTableAsync<TTargetTable>(Expression<Func<TTargetTable, bool>> match, TTargetTable model, CancellationToken cancellationToken)
        where TTargetTable : class
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            var existingModel = await dbContext.Set<TTargetTable>()
                .FirstOrDefaultAsync(match, cancellationToken);

            if (existingModel != null)
            {
                dbContext.Entry(existingModel).CurrentValues.SetValues(model);
                dbContext.Update(existingModel);
            }
            else
            {
                dbContext.Add(model);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

    }

    public async Task DeleteAsync(IIdentity id, CancellationToken cancellationToken)
    {
        await DeleteTableAsync(id.Value, cancellationToken);
    }

    public async Task DeleteTableAsync(string id, CancellationToken cancellationToken)
    {
        await DeleteTableAsync<TStateTable>(id, cancellationToken);
    }

    public async Task DeleteTableAsync<TTargetTable>(string id, CancellationToken cancellationToken)
        where TTargetTable : class
    {
        using (var dbContext = contextProvider.CreateContext())
        {
            var entity = await dbContext.Set<TTargetTable>().FindAsync(id);
            if (entity == null)
                return;

            dbContext.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AllCommittedStatesPage> LoadAllCommittedEvents(GlobalPosition globalPosition, int pageSize, CancellationToken cancellationToken)
    {
        var startPosition = globalPosition.IsStart
                ? 0
                : long.Parse(globalPosition.Value);

        using (var dbContext = contextProvider.CreateContext())
        {
            var entities = await dbContext.Set<TStateTable>()
                .AsNoTracking()
                .OrderBy(e => e.GlobalSequenceNumber)
                .Where(e => e.GlobalSequenceNumber >= startPosition)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var nextPosition = entities.Any()
                ? entities.Max(e => e.GlobalSequenceNumber) + 1
                : startPosition;

            var results = entities.Select(table => ToHydatedEvent(table.Id, ToDto(table))).ToList();

            return new AllCommittedStatesPage(new GlobalPosition(nextPosition.ToString()), results);
        }
    }

    protected abstract TStateModel ToDto(TStateTable table);
    protected abstract TStateTable ToTable(string id, TStateModel dto, long version);
    protected abstract IEntityHydratedEvent ToHydatedEvent(string id, TStateModel model);
}
