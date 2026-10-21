using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPatch.Catalog.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Vuelve a publicar todas las categorías con su descripción (ADR-025, quickpatch-kafka v0.8.0); ver
    /// <see cref="CategoryResync"/>. No cambia el esquema.
    /// </summary>
    public partial class PublicarDescripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CategoryResync.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Los eventos ya publicados no se retiran.
        }
    }
}
