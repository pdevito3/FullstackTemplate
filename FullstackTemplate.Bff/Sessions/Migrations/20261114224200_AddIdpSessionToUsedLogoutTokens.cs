using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Bff.Sessions.Migrations
{
    /// <inheritdoc />
    public partial class AddIdpSessionToUsedLogoutTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "issued_at",
                table: "used_logout_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "session_id",
                table: "used_logout_tokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "used_logout_tokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_used_logout_tokens_session_id",
                table: "used_logout_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_used_logout_tokens_subject",
                table: "used_logout_tokens",
                column: "subject");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_used_logout_tokens_session_id",
                table: "used_logout_tokens");

            migrationBuilder.DropIndex(
                name: "ix_used_logout_tokens_subject",
                table: "used_logout_tokens");

            migrationBuilder.DropColumn(
                name: "issued_at",
                table: "used_logout_tokens");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "used_logout_tokens");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "used_logout_tokens");
        }
    }
}
