namespace QuickPatch.Catalog.Api.Security;

/// <summary>
/// Tenant del canal (sección <c>Channel</c>), el mismo que usa Identity en el registro y el login (RN-U5). Solo lo usa
/// la lista de categorías sin token, para que la app muestre las especialidades en el registro de técnicos (RF-02,
/// contrato 1.2.0). En el MVP opera un solo tenant (DD 10.3); vacío, la lista sin token responde 503.
/// </summary>
public sealed class ChannelOptions
{
    public const string Section = "Channel";

    public Guid TenantId { get; set; }
}