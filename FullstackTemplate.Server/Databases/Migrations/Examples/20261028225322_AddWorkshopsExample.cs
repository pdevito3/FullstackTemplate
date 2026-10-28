using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations.Examples
{
    /// <inheritdoc />
    public partial class AddWorkshopsExample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_venues", x => x.id);
                    table.ForeignKey(
                        name: "fk_venues_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "workshops",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    venue_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshops", x => x.id);
                    table.ForeignKey(
                        name: "fk_workshops_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_workshops_venues_venue_id",
                        column: x => x.venue_id,
                        principalTable: "venues",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_venues_tenant_id_name_id",
                table: "venues",
                columns: new[] { "tenant_id", "name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_workshops_tenant_id_starts_at_id",
                table: "workshops",
                columns: new[] { "tenant_id", "starts_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_workshops_venue_id",
                table: "workshops",
                column: "venue_id");

            migrationBuilder.GrantPermissionToRole("Admin", "manage_workshops");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeletePermission("manage_workshops");

            migrationBuilder.DropTable(
                name: "workshops");

            migrationBuilder.DropTable(
                name: "venues");
        }
    }
}
