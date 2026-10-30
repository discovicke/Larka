using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTeachers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseTeachers",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTeachers", x => new { x.CourseId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CourseTeachers_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseTeachers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseTeachers_UserId",
                table: "CourseTeachers",
                column: "UserId");

            // Ingen ska tappa behörighet vid driftsättningen. Förut var den som är lärare i en klass lärare i alla
            // kurser klassen läser, så varje sådant par av användare och kurs blir en rad. Klassens lärare tas
            // inte bort ur klassen, det gör bara att de inte längre räknas som kursens lärare via den.
            migrationBuilder.Sql("""
                INSERT INTO "CourseTeachers" ("CourseId", "UserId")
                SELECT DISTINCT groupCourse."CourseId", enrollment."UserId"
                FROM "Enrollments" AS enrollment
                JOIN "GroupCourses" AS groupCourse ON groupCourse."GroupId" = enrollment."GroupId"
                WHERE enrollment."Role" = 'Teacher'
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseTeachers");
        }
    }
}
