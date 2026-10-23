using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddClientToAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "agent_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "agent_permissions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "agent_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "agent_permissions");
        }
    }
}
