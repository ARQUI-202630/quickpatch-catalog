using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Application.Events;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Categories;

/// <summary>Categorías activas del tenant, ordenadas por nombre (<c>GET /v1/catalog/categories</c>).</summary>
public sealed class ListCategoriesHandler(ITenantUnitOfWork unitOfWork, ICategoryRepository categories)
{
    public Task<IReadOnlyList<Category>> HandleAsync(Guid tenantId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(tenantId, categories.ListActiveAsync, cancellationToken);
}

public sealed record CreateCategoryCommand(Guid TenantId, Guid CorrelationId, string? Name, string? Description);

public abstract record CreateCategoryResult
{
    public sealed record Created(Category Category) : CreateCategoryResult;

    public sealed record NameTaken : CreateCategoryResult;
}

/// <summary>Alta de categoría por <c>admin_tenant</c>; publica <c>catalog.category-changed</c> en la misma transacción.</summary>
public sealed class CreateCategoryHandler(
    ITenantUnitOfWork unitOfWork,
    ICategoryRepository categories,
    IOutbox outbox,
    TimeProvider clock)
{
    public async Task<CreateCategoryResult> HandleAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var now = clock.GetUtcNow();
        var category = Category.Create(command.TenantId, command.Name, command.Description, now);

        try
        {
            return await unitOfWork.ExecuteAsync<CreateCategoryResult>(
                command.TenantId,
                async ct =>
                {
                    if (await categories.NameExistsAsync(Category.NormalizeName(category.Name), ct))
                    {
                        return new CreateCategoryResult.NameTaken();
                    }

                    categories.Add(category);
                    outbox.Enqueue(CategoryChangedEvent.From(category, command.CorrelationId, now));
                    return new CreateCategoryResult.Created(category);
                },
                cancellationToken);
        }
        catch (DuplicateCategoryNameException)
        {
            return new CreateCategoryResult.NameTaken();
        }
    }
}

public sealed record UpdateCategoryCommand(Guid TenantId, Guid CorrelationId, Guid CategoryId, string? Description, bool? Active);

/// <summary>
/// Cambio de descripción o de estado por <c>admin_tenant</c>. Si algo cambió, publica el estado completo; una
/// categoría de otro tenant no se encuentra (RLS) y la API responde 404.
/// </summary>
public sealed class UpdateCategoryHandler(
    ITenantUnitOfWork unitOfWork,
    ICategoryRepository categories,
    IOutbox outbox,
    TimeProvider clock)
{
    public Task<Category?> HandleAsync(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return unitOfWork.ExecuteAsync<Category?>(
            command.TenantId,
            async ct =>
            {
                var category = await categories.FindAsync(command.CategoryId, ct);
                if (category is null)
                {
                    return null;
                }

                var now = clock.GetUtcNow();
                if (category.Update(command.Description, command.Active, now))
                {
                    outbox.Enqueue(CategoryChangedEvent.From(category, command.CorrelationId, now));
                }

                return category;
            },
            cancellationToken);
    }
}