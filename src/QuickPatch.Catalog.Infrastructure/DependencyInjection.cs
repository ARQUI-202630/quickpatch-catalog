using Confluent.Kafka;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using QuickPatch.Catalog.Application.Abstractions;
using QuickPatch.Catalog.Infrastructure.Messaging;
using QuickPatch.Catalog.Infrastructure.Options;
using QuickPatch.Catalog.Infrastructure.Persistence;

namespace QuickPatch.Catalog.Infrastructure;

/// <summary>
/// Capa de infraestructura: PostgreSQL (EF Core) y Kafka (SDD 6.3).
/// Implementa las interfaces que definen Application y Domain.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Catalog";
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.Section));
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.Section));

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(configuration.GetConnectionString(ConnectionStringName)));

        services.AddScoped<ITenantUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOutbox, EfOutbox>();

        services.AddHealthChecks().AddDbContextCheck<CatalogDbContext>("postgresql", tags: [ReadyTag]);

        var kafka = configuration.GetSection(KafkaOptions.Section).Get<KafkaOptions>() ?? new KafkaOptions();
        if (kafka.Enabled)
        {
            services.AddSingleton(_ => new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = kafka.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
            }).Build());

            services.AddSingleton<OutboxPublisher>();
            services.AddHostedService(sp => sp.GetRequiredService<OutboxPublisher>());
        }

        return services;
    }
}