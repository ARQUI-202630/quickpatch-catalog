using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Abstractions;

/// <summary>Persistencia de categorías (tabla <c>service_categories</c>).</summary>
public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken);

    Task<Category?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken);

    void Add(Category category);
}