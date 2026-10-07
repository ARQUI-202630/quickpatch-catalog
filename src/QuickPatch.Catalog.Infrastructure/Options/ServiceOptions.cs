namespace QuickPatch.Catalog.Infrastructure.Options;

/// <summary>Conexión a Kafka (VM6). Sección <c>Kafka</c>; sin servidores, el publicador del Outbox no arranca.</summary>
public sealed class KafkaOptions
{
    public const string Section = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(BootstrapServers);
}

/// <summary>Publicador del Outbox (ADR-007). Sección <c>Outbox</c>.</summary>
public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>
    /// Rol <c>BYPASSRLS</c> que adopta el publicador con <c>SET LOCAL ROLE</c> para leer los eventos de todos
    /// los tenants (DD, sección 10.2). Vacío solo en desarrollo local.
    /// </summary>
    public string PublisherRole { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 50;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}