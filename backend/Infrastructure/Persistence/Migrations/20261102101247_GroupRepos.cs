using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupRepos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentGroupId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentGroupId",
                table: "StudentRepos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssignmentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentGroups_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentGroupMembers",
                columns: table => new
                {
                    AssignmentGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentGroupMembers", x => new { x.AssignmentGroupId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_AssignmentGroupMembers_AssignmentGroups_AssignmentGroupId",
                        column: x => x.AssignmentGroupId,
                        principalTable: "AssignmentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentGroupMembers_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_AssignmentGroupId",
                table: "Submissions",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRepos_AssignmentGroupId",
                table: "StudentRepos",
                column: "AssignmentGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentGroupMembers_StudentId",
                table: "AssignmentGroupMembers",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentGroups_ContentLinkId_Position",
                table: "AssignmentGroups",
                columns: new[] { "ContentLinkId", "Position" });

            migrationBuilder.AddForeignKey(
                name: "FK_StudentRepos_AssignmentGroups_AssignmentGroupId",
                table: "StudentRepos",
                column: "AssignmentGroupId",
                principalTable: "AssignmentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_AssignmentGroups_AssignmentGroupId",
                table: "Submissions",
                column: "AssignmentGroupId",
                principalTable: "AssignmentGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentRepos_AssignmentGroups_AssignmentGroupId",
                table: "StudentRepos");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_AssignmentGroups_AssignmentGroupId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "AssignmentGroupMembers");

            migrationBuilder.DropTable(
                name: "AssignmentGroups");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_AssignmentGroupId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_StudentRepos_AssignmentGroupId",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "StudentRepos");
        }
    }
}
