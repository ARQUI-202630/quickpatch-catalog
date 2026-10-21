namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>
/// Vuelve a publicar todas las categorías una vez (ADR-025): desde quickpatch-kafka v0.8.0 el evento lleva la descripción y
/// ServiceRequest sirve la lista desde su réplica. Marca cada categoría como cambiada ahora, porque la réplica solo acepta un
/// <c>updatedAt</c> más reciente (ADR-017), y deja en el Outbox un <c>catalog.category-changed</c> con el estado completo, igual
/// al que arma <c>CategoryChangedEvent</c>. La migración corre con el rol dueño de las tablas, que no está sujeto a RLS.
/// </summary>
public static class CategoryResync
{
    public const string Sql = """
        UPDATE service_categories SET updated_at = now();

        INSERT INTO outbox_events (id, tenant_id, aggregate_id, event_type, payload, created_at, attempts)
        SELECT e.id, c.tenant_id, c.id, 'catalog.category-changed',
               jsonb_build_object(
                   'eventId', e.id,
                   'eventType', 'catalog.category-changed',
                   'eventVersion', 1,
                   'occurredAt', c.updated_at,
                   'correlationId', e.id,
                   'tenantId', c.tenant_id,
                   'producer', 'catalog-service',
                   'data', jsonb_build_object(
                       'categoryId', c.id,
                       'name', c.name,
                       'description', c.description,
                       'active', c.active,
                       'updatedAt', c.updated_at)),
               now(), 0
        FROM service_categories c
        CROSS JOIN LATERAL (SELECT gen_random_uuid() AS id) e;
        """;
}