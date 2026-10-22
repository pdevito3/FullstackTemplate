using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Bff.Sessions.Migrations
{
    /// <inheritdoc />
    public partial class AddUsedLogoutTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "used_logout_tokens",
                columns: table => new
                {
                    jti = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, collation: "C"),
                    expires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_used_logout_tokens", x => x.jti);
                });

            migrationBuilder.CreateIndex(
                name: "ix_used_logout_tokens_expires",
                table: "used_logout_tokens",
                column: "expires");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "used_logout_tokens");
        }
    }
}
