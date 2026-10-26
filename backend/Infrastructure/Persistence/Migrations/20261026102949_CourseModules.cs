using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CourseModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                table: "ContentLinks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Modules_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_ModuleId",
                table: "ContentLinks",
                column: "ModuleId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ContentLinks_ModuleNeedsCourse",
                table: "ContentLinks",
                sql: "\"ModuleId\" IS NULL OR \"CourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_CourseId_Position",
                table: "Modules",
                columns: new[] { "CourseId", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentLinks_Modules_ModuleId",
                table: "ContentLinks",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentLinks_Modules_ModuleId",
                table: "ContentLinks");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropIndex(
                name: "IX_ContentLinks_ModuleId",
                table: "ContentLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ContentLinks_ModuleNeedsCourse",
                table: "ContentLinks");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "ContentLinks");
        }
    }
}
