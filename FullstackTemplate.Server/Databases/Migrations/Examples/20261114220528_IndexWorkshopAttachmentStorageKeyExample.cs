using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FullstackTemplate.Server.Databases.Migrations.Examples
{
    /// <inheritdoc />
    public partial class IndexWorkshopAttachmentStorageKeyExample : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_workshop_attachments_storage_key",
                table: "workshop_attachments",
                column: "storage_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workshop_attachments_storage_key",
                table: "workshop_attachments");
        }
    }
}
