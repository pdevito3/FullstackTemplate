using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class DropWhoAndWhenColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_modified_by",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "users");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "user_permissions");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "last_modified_by",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "last_modified_by",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "created_by",
                table: "agent_permissions");

            migrationBuilder.DropColumn(
                name: "created_by_client",
                table: "agent_permissions");

            migrationBuilder.DropColumn(
                name: "created_on",
                table: "agent_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by",
                table: "agent_permissions");

            migrationBuilder.DropColumn(
                name: "last_modified_by_client",
                table: "agent_permissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_on",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_on",
                table: "user_permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "user_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_on",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_on",
                table: "agents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by",
                table: "agent_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_client",
                table: "agent_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_on",
                table: "agent_permissions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by",
                table: "agent_permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_modified_by_client",
                table: "agent_permissions",
                type: "text",
                nullable: true);
        }
    }
}
