using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuickPatch.Catalog.Infrastructure.Persistence;

/// <summary>Fábrica para <c>dotnet ef</c> (generar migraciones). No se conecta a ninguna base.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql("Host=localhost;Database=db_catalog").Options);
}