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
            // A row is an object in storage that nothing else deletes, so a drop with rows would keep the object
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM object_delete_requests) THEN
                        RAISE EXCEPTION 'object_delete_requests has rows. Run the previous version until it drains them, and handle its dead rows.';
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
