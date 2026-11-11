namespace QuickPatch.Catalog.Infrastructure.Options;

/// <summary>Conexión a Kafka (VM6). Sección <c>Kafka</c>; sin servidores, el publicador del Outbox no arranca.</summary>
public sealed class KafkaOptions
{
    public const string Section = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(BootstrapServers);
}