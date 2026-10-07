using Microsoft.EntityFrameworkCore;

using Npgsql;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>
/// Transacción por tenant (DD 10.2): fija <c>app.current_tenant</c> con <c>set_config(..., true)</c>,
/// equivalente a <c>SET LOCAL</c>, antes de ejecutar el trabajo; RLS filtra todo lo que se lea o escriba.
/// </summary>
public sealed class TenantUnitOfWork(CatalogDbContext db) : ITenantUnitOfWork
{
    public const string UniqueNameIndex = "ux_service_categories_tenant_name";

    public async Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var tenant = tenantId.ToString();
        await db.Database.ExecuteSqlAsync($"SELECT set_config('app.current_tenant', {tenant}, true)", cancellationToken);

        var result = await work(cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: UniqueNameIndex })
        {
            throw new DuplicateCategoryNameException("Ya existe una categoría con ese nombre en el tenant.", ex);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

public sealed class CategoryRepository(CatalogDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Categories.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<Category?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Categories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
#pragma warning disable CA1862 // EF Core traduce ToLower() a lower() en PostgreSQL; coincide con el índice único.
        db.Categories.AnyAsync(x => x.Name.ToLower() == normalizedName, cancellationToken);
#pragma warning restore CA1862

    public void Add(Category category) => db.Categories.Add(category);
}

public sealed class EfOutbox(CatalogDbContext db, TimeProvider clock) : IOutbox
{
    public void Enqueue(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.OutboxEvents.Add(new OutboxEventRecord
        {
            Id = message.Id,
            TenantId = message.TenantId,
            AggregateId = message.AggregateId,
            EventType = message.EventType,
            Payload = message.Payload,
            CreatedAt = clock.GetUtcNow(),
            Attempts = 0,
        });
    }
}