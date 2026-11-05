using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    require_email_match = table.Column<bool>(type: "boolean", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    claimed_by_user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "C"),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_modified_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_invites_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_invites_users_claimed_by_user_id",
                        column: x => x.claimed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_invites_claimed_by_user_id",
                table: "invites",
                column: "claimed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_invites_tenant_id_expires_at_id",
                table: "invites",
                columns: new[] { "tenant_id", "expires_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_invites_token_hash",
                table: "invites",
                column: "token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invites");
        }
    }
}
