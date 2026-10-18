using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class UserListSortIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id_last_name_first_name_id",
                table: "users",
                columns: new[] { "tenant_id", "last_name", "first_name", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id_last_name_first_name_id",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id",
                table: "users",
                column: "tenant_id");
        }
    }
}
