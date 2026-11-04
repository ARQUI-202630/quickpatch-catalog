using System.Text.Json;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Application.Events;

/// <summary>Sobre común de los eventos (DD 8.2.1; <c>quickpatch-kafka/events</c>).</summary>
public sealed record EventEnvelope<TData>(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid TenantId,
    string Producer,
    TData Data);

/// <summary>
/// <c>data</c> de <c>catalog.category-changed</c> v1. <see cref="Description"/> es opcional desde quickpatch-kafka v0.8.0:
/// ServiceRequest sirve la lista de categorías desde su réplica (ADR-025).
/// </summary>
public sealed record CategoryChangedData(Guid CategoryId, string Name, string? Description, bool Active, DateTimeOffset UpdatedAt);

public static class CategoryChangedEvent
{
    public const string Type = "catalog.category-changed";
    public const string Producer = "catalog-service";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Estado completo de la categoría (event-carried state transfer), listo para el Outbox.</summary>
    public static OutboxMessage From(Category category, Guid correlationId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        var eventId = Guid.CreateVersion7(now);
        var envelope = new EventEnvelope<CategoryChangedData>(
            eventId,
            Type,
            1,
            now,
            correlationId,
            category.TenantId,
            Producer,
            new CategoryChangedData(category.Id, category.Name, category.Description, category.Active, category.UpdatedAt));
        return new OutboxMessage(eventId, category.TenantId, category.Id, Type, JsonSerializer.Serialize(envelope, Json));
    }
}