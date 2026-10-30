using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSupportSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckStatusChangedAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InitialCommitAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialSha",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProvisionedAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudentHelpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentHelpRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentHelpRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_ContentLinkId_StudentId",
                table: "StudentHelpRequests",
                columns: new[] { "ContentLinkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_ResolvedById",
                table: "StudentHelpRequests",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHelpRequests_StudentId",
                table: "StudentHelpRequests",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentHelpRequests");

            migrationBuilder.DropColumn(
                name: "CheckStatusChangedAt",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "InitialCommitAt",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "InitialSha",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "ProvisionedAt",
                table: "StudentRepos");
        }
    }
}
