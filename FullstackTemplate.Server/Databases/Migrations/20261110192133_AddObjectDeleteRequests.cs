using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddObjectDeleteRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "object_delete_requests",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_prefix = table.Column<bool>(type: "boolean", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    dead_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "object_delete_requests");
        }
    }
}
