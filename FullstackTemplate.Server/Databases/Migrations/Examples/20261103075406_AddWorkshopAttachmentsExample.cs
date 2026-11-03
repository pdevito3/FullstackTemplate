using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations.Examples
{
    /// <inheritdoc />
    public partial class AddWorkshopAttachmentsExample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workshop_attachments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    workshop_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshop_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_workshop_attachments_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_workshop_attachments_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_workshop_attachments_tenant_id",
                table: "workshop_attachments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_workshop_attachments_workshop_id_uploaded_at_id",
                table: "workshop_attachments",
                columns: new[] { "workshop_id", "uploaded_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workshop_attachments");
        }
    }
}
