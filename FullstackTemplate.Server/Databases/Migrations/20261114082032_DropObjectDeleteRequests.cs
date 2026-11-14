using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class DropObjectDeleteRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The start of the Server moves the rows onto BackWave before this migration, in ObjectDeleteRequestsDrain.
            // A row here came after that, from a Server of the previous version, or the migration runs with no start.
            // A row names objects in storage that nothing else deletes, so a drop with rows would keep them.
            // The lock waits for each open write of the queue, so the check sees a row of a save that commits late.
            migrationBuilder.Sql("""
                LOCK TABLE object_delete_requests IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM object_delete_requests) THEN
                        RAISE EXCEPTION 'object_delete_requests has rows. Start the Server, which moves them onto BackWave before this migration.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropTable(
                name: "object_delete_requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "object_delete_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    dead_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_prefix = table.Column<bool>(type: "boolean", nullable: false),
                    key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    trace_parent = table.Column<string>(type: "character varying(55)", maxLength: 55, nullable: true),
                    trace_state = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_object_delete_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_object_delete_requests_due_at_id",
                table: "object_delete_requests",
                columns: new[] { "due_at", "id" },
                filter: "dead_at IS NULL");
        }
    }
}
