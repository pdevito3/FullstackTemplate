using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FullstackTemplate.Bff.Sessions.Migrations
{
    /// <inheritdoc />
    public partial class InitialBffSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: false, collation: "C"),
                    subject_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    session_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    renewed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ticket = table.Column<byte[]>(type: "bytea", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessions", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sessions_absolute_expires",
                table: "sessions",
                column: "absolute_expires");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_expires",
                table: "sessions",
                column: "expires");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_session_id",
                table: "sessions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_subject_id",
                table: "sessions",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "sessions");
        }
    }
}
