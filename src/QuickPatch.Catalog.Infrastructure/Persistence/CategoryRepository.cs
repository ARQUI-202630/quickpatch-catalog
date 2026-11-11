using Microsoft.EntityFrameworkCore;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>Repositorio de categorías sobre EF Core; RLS limita las consultas al tenant de la transacción.</summary>
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