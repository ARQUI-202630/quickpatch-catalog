using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuickPatch.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_events_pending",
                table: "outbox_events",
                column: "created_at",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_categories_tenant_active",
                table: "service_categories",
                columns: new[] { "tenant_id", "active" });

            // Nombre único por tenant sin distinguir mayúsculas (DD 5.4) y aislamiento por tenant con RLS (DD 10.2).
            migrationBuilder.Sql(SqlAdicional);
        }

        private const string CurrentTenant = "NULLIF(current_setting('app.current_tenant', true), '')::uuid";

        private static readonly string SqlAdicional = $"""
            CREATE UNIQUE INDEX ux_service_categories_tenant_name ON service_categories (tenant_id, lower(name));

            ALTER TABLE service_categories ENABLE ROW LEVEL SECURITY;
            ALTER TABLE service_categories FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation ON service_categories USING (tenant_id = {CurrentTenant}) WITH CHECK (tenant_id = {CurrentTenant});

            -- outbox_events: el servicio solo inserta con el tenant de la sesión; lee y marca como publicado
            -- únicamente el rol del publicador, que tiene BYPASSRLS.
            ALTER TABLE outbox_events ALTER COLUMN tenant_id SET DEFAULT {CurrentTenant};
            ALTER TABLE outbox_events ENABLE ROW LEVEL SECURITY;
            ALTER TABLE outbox_events FORCE ROW LEVEL SECURITY;
            CREATE POLICY outbox_insert ON outbox_events FOR INSERT WITH CHECK (tenant_id = {CurrentTenant});
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "service_categories");
        }
    }
}
