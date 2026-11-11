using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Application.Events;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Categories;

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