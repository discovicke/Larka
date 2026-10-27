using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoadmapAndCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletionRule",
                table: "AssignmentSpecs",
                type: "text",
                nullable: false,
                defaultValue: "Submitted");

            migrationBuilder.CreateTable(
                name: "ContentLinkDependencies",
                columns: table => new
                {
                    BlockedLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockingLinkId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentLinkDependencies", x => new { x.BlockedLinkId, x.BlockingLinkId });
                    table.CheckConstraint("CK_ContentLinkDependencies_NotSelf", "\"BlockedLinkId\" <> \"BlockingLinkId\"");
                    table.ForeignKey(
                        name: "FK_ContentLinkDependencies_ContentLinks_BlockedLinkId",
                        column: x => x.BlockedLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentLinkDependencies_ContentLinks_BlockingLinkId",
                        column: x => x.BlockingLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PartCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartCompletions_ContentLinks_ContentLinkId",
                        column: x => x.ContentLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartCompletions_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinkDependencies_BlockingLinkId",
                table: "ContentLinkDependencies",
                column: "BlockingLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_PartCompletions_ContentLinkId_StudentId",
                table: "PartCompletions",
                columns: new[] { "ContentLinkId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartCompletions_StudentId",
                table: "PartCompletions",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentLinkDependencies");

            migrationBuilder.DropTable(
                name: "PartCompletions");

            migrationBuilder.DropColumn(
                name: "CompletionRule",
                table: "AssignmentSpecs");
        }
    }
}
