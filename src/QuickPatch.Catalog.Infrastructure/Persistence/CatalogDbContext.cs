using Microsoft.EntityFrameworkCore;

using QuickPatch.Catalog.Domain.Categories;
using QuickPatch.Transversal.Messaging;

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

        // Tablas transversales con el mapeo común (ADR-020); las migraciones siguen siendo de este servicio.
        modelBuilder.MapOutboxEvents();
    }
}