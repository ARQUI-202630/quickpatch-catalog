using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Abstractions;

/// <summary>
/// Ejecuta un trabajo en una transacción que primero fija el tenant de la sesión (<c>SET LOCAL app.current_tenant</c>,
/// DD 10.2) para que RLS aísle los datos. Al terminar guarda los cambios y confirma.
/// </summary>
public interface ITenantUnitOfWork
{
    Task<T> ExecuteAsync<T>(Guid tenantId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}

/// <summary>Persistencia de categorías (tabla <c>service_categories</c>).</summary>
public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListActiveAsync(CancellationToken cancellationToken);

    Task<Category?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken);

    void Add(Category category);
}

/// <summary>Transactional Outbox (ADR-007): el evento se guarda en la misma transacción que el cambio.</summary>
public interface IOutbox
{
    void Enqueue(OutboxMessage message);
}

/// <summary>Mensaje pendiente de publicar en Kafka. <see cref="Id"/> se publica como <c>eventId</c>.</summary>
public sealed record OutboxMessage(Guid Id, Guid TenantId, Guid AggregateId, string EventType, string Payload);

/// <summary>El nombre ya existe en el tenant (unicidad de DD 5.4 en la base de datos).</summary>
public sealed class DuplicateCategoryNameException : Exception
{
    public DuplicateCategoryNameException()
        : base("Ya existe una categoría con ese nombre en el tenant.")
    {
    }

    public DuplicateCategoryNameException(string message)
        : base(message)
    {
    }

    public DuplicateCategoryNameException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}