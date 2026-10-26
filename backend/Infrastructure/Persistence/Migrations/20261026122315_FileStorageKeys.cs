using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FileStorageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Url",
                table: "SubmissionFiles",
                newName: "StorageKey");

            migrationBuilder.RenameColumn(
                name: "Url",
                table: "Attachments",
                newName: "StorageKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StorageKey",
                table: "SubmissionFiles",
                newName: "Url");

            migrationBuilder.RenameColumn(
                name: "StorageKey",
                table: "Attachments",
                newName: "Url");
        }
    }
}
