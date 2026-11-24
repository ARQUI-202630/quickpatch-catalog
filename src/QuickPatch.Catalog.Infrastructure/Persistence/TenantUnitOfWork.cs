using Microsoft.EntityFrameworkCore;

using Npgsql;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Transversal.Persistence;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>
/// Transacción por tenant (DD 10.2) con la implementación común de ADR-020. Traduce el índice único del nombre de la
/// categoría a su excepción de dominio.
/// </summary>
public sealed class TenantUnitOfWork(CatalogDbContext db) : ITenantUnitOfWork
{
    public const string UniqueNameIndex = "ux_service_categories_tenant_name";

    public async Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            return await TenantTransaction.ExecuteAsync(db, tenantId, work, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: UniqueNameIndex })
        {
            throw new DuplicateCategoryNameException("Ya existe una categoría con ese nombre en el tenant.", ex);
        }
    }
}