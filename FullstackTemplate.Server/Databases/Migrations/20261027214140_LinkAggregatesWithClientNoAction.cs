using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class LinkAggregatesWithClientNoAction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agents_tenants_tenant_id",
                table: "agents");

            migrationBuilder.DropForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users");

            migrationBuilder.AddForeignKey(
                name: "fk_agents_tenants_tenant_id",
                table: "agents",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_agents_tenants_tenant_id",
                table: "agents");

            migrationBuilder.DropForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users");

            migrationBuilder.AddForeignKey(
                name: "fk_agents_tenants_tenant_id",
                table: "agents",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_tenants_tenant_id",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
