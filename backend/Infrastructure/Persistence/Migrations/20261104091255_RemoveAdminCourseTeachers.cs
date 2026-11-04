using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminCourseTeachers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bara användare med rollen Lärare undervisar i en kurs (#492). Admin som har lagt till sig själv som
            // lärare tas bort ur kursen. Rollen lagras som text. Betyg och inlämningar ligger kvar.
            migrationBuilder.Sql(
                """
                DELETE FROM "CourseTeachers"
                WHERE "UserId" IN (SELECT "Id" FROM "Users" WHERE "Role" <> 'Teacher');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Borttagna lärarrader går inte att återskapa.
        }
    }
}
