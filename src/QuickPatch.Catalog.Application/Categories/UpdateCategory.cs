using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Application.Events;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Categories;

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