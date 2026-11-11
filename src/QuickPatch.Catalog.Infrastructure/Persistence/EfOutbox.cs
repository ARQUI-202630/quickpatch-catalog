using QuickPatch.Catalog.Application.Abstractions;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>Outbox en <c>outbox_events</c>, en la misma transacción que el cambio (ADR-007).</summary>
public sealed class EfOutbox(CatalogDbContext db, TimeProvider clock) : IOutbox
{
    public void Enqueue(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.OutboxEvents.Add(new OutboxEventRecord
        {
            Id = message.Id,
            TenantId = message.TenantId,
            AggregateId = message.AggregateId,
            EventType = message.EventType,
            Payload = message.Payload,
            CreatedAt = clock.GetUtcNow(),
            Attempts = 0,
        });
    }
}