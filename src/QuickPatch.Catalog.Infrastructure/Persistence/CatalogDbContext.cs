using Microsoft.EntityFrameworkCore;

using QuickPatch.Catalog.Domain.Categories;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>Base de datos de Catalog Service (DD, secciones 5.4 y 5.15).</summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("service_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(Category.NameMaxLength);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(Category.DescriptionMaxLength);
            e.Property(x => x.Active).HasColumnName("active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.TenantId, x.Active }).HasDatabaseName("ix_service_categories_tenant_active");
        });

        modelBuilder.Entity<OutboxEventRecord>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.TenantId).HasColumnName("tenant_id");
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_outbox_events_pending").HasFilter("published_at IS NULL");
        });
    }
}

/// <summary>Fila de <c>outbox_events</c> (DD 5.15).</summary>
public sealed class OutboxEventRecord
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid AggregateId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int Attempts { get; set; }
}