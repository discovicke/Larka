using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssignmentReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetLinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentReferences_ContentLinks_SourceLinkId",
                        column: x => x.SourceLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentReferences_ContentLinks_TargetLinkId",
                        column: x => x.TargetLinkId,
                        principalTable: "ContentLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_SourceLinkId_Position",
                table: "AssignmentReferences",
                columns: new[] { "SourceLinkId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_SourceLinkId_TargetLinkId",
                table: "AssignmentReferences",
                columns: new[] { "SourceLinkId", "TargetLinkId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentReferences_TargetLinkId",
                table: "AssignmentReferences",
                column: "TargetLinkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentReferences");
        }
    }
}
