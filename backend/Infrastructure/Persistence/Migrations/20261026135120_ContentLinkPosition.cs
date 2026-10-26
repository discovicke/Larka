using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentLinkPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentLinks_ModuleId",
                table: "ContentLinks");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "ContentLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Befintliga länkar får den ordning de visades i innan Position fanns.
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT link."Id", row_number() OVER (
                        PARTITION BY link."ModuleId" ORDER BY link."PublishAt", content."Title", link."Id"
                    ) AS "Position"
                    FROM "ContentLinks" AS link
                    JOIN "Content" AS content ON content."Id" = link."ContentId"
                    WHERE link."ModuleId" IS NOT NULL
                )
                UPDATE "ContentLinks" AS link
                SET "Position" = numbered."Position"
                FROM numbered
                WHERE link."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_ModuleId_Position",
                table: "ContentLinks",
                columns: new[] { "ModuleId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContentLinks_ModuleId_Position",
                table: "ContentLinks");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "ContentLinks");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLinks_ModuleId",
                table: "ContentLinks",
                column: "ModuleId");
        }
    }
}
