using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGroupTerm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Groups_SchoolId_Name_Term",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "Groups");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SchoolId_Name",
                table: "Groups",
                columns: new[] { "SchoolId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Groups_SchoolId_Name",
                table: "Groups");

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "Groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SchoolId_Name_Term",
                table: "Groups",
                columns: new[] { "SchoolId", "Name", "Term" },
                unique: true);
        }
    }
}
