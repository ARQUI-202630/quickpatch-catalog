using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.UnitTests.Support;

/// <summary>Puertos en memoria; emula RLS: solo se ven las categorías del tenant de la transacción en curso.</summary>
public sealed class InMemoryCatalog : ITenantUnitOfWork, ICategoryRepository, IOutbox
{
    private Guid? currentTenant;

    public List<Category> Categories { get; } = [];

    public List<OutboxMessage> Outbox { get; } = [];

    public bool FailNextSaveWithDuplicate { get; set; }

    public async Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        currentTenant = tenantId;
        try
        {
            var result = await work(cancellationToken);
            if (FailNextSaveWithDuplicate)
            {
                FailNextSaveWithDuplicate = false;
                throw new DuplicateCategoryNameException();
            }

            return result;
        }
        finally
        {
            currentTenant = null;
        }
    }

    public Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Category>>(
            [.. Categories.Where(c => c.TenantId == currentTenant && c.Active).OrderBy(c => c.Name, StringComparer.Ordinal)]);

    public Task<Category?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.SingleOrDefault(c => c.TenantId == currentTenant && c.Id == id));

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken) =>
        Task.FromResult(Categories.Any(c => c.TenantId == currentTenant && Category.NormalizeName(c.Name) == normalizedName));

    public void Add(Category category) => Categories.Add(category);

    public void Enqueue(OutboxMessage message) => Outbox.Add(message);
}

public sealed class MutableClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}