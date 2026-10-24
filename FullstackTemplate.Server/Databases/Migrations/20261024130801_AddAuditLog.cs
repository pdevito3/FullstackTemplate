using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    actor_client = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    actor_user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "C"),
                    actor_agent_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, collation: "C"),
                    request_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    trace_id = table.Column<string>(type: "character(32)", fixedLength: true, maxLength: 32, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    root_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    root_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, collation: "C"),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entries", x => new { x.occurred_at, x.record_id, x.ordinal });
                    table.CheckConstraint("ck_audit_entries_action_names_an_action", "(kind = 'Action') = (action IS NOT NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_root_id_occurred_at_record_id_ordinal",
                table: "audit_entries",
                columns: new[] { "root_id", "occurred_at", "record_id", "ordinal" },
                descending: new[] { false, true, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id_occurred_at_record_id_ordinal",
                table: "audit_entries",
                columns: new[] { "tenant_id", "occurred_at", "record_id", "ordinal" },
                descending: new[] { false, true, true, true });

            // The audit log is append-only. DROP and a later DETACH of a partition are DDL, so they stay possible.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_entries_refuse_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_entries is append-only, so % is not allowed.', TG_OP;
                END
                $$;

                CREATE TRIGGER audit_entries_refuse_update_and_delete
                    BEFORE UPDATE OR DELETE ON audit_entries
                    FOR EACH ROW EXECUTE FUNCTION audit_entries_refuse_change();

                CREATE TRIGGER audit_entries_refuse_truncate
                    BEFORE TRUNCATE ON audit_entries
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_entries_refuse_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.Sql("DROP FUNCTION audit_entries_refuse_change();");
        }
    }
}
