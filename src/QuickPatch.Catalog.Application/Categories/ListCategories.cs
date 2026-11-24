using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Categories;

/// <summary>Categorías activas del tenant, ordenadas por nombre (<c>GET /v1/catalog/categories</c>).</summary>
public sealed class ListCategoriesHandler(ITenantUnitOfWork unitOfWork, ICategoryRepository categories)
{
    public Task<IReadOnlyList<Category>> HandleAsync(Guid tenantId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(tenantId, categories.ListActiveAsync, cancellationToken);
}