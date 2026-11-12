using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Transversal.Messaging;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>Outbox en <c>outbox_events</c>, en la misma transacción que el cambio (ADR-007).</summary>
public sealed class EfOutbox(CatalogDbContext db, TimeProvider clock) : IOutbox
{
    public void Enqueue(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.OutboxEvents.Add(OutboxEventRecord.Pending(message.Id, message.TenantId, message.AggregateId, message.EventType, message.Payload, clock.GetUtcNow()));
    }
}