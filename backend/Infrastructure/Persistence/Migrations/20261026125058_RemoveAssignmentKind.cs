using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssignmentKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Betygsskalan säger redan om uppgiften är en övning (None) eller en inlämning.
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AssignmentSpecs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AssignmentSpecs",
                type: "text",
                nullable: false,
                defaultValue: "Exercise");

            migrationBuilder.Sql("""
                UPDATE "AssignmentSpecs" SET "Kind" = 'Lab' WHERE "GradeScale" <> 'None';
                """);
        }
    }
}
