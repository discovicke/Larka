using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OneNotificationPerPublishAndDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dubbletter som redan finns skulle stoppa de unika indexen. Den äldsta notisen behålls.
            migrationBuilder.Sql(
                """
                DELETE FROM "Notifications" AS later
                USING "Notifications" AS earlier
                WHERE later."Kind" = earlier."Kind"
                  AND later."ContentId" = earlier."ContentId"
                  AND later."UserId" = earlier."UserId"
                  AND (later."Kind" IN ('NewsPublished', 'AssignmentPublished')
                       OR (later."Kind" = 'DeadlineSoon' AND later."DueAt" = earlier."DueAt"))
                  AND (later."CreatedAt", later."Id") > (earlier."CreatedAt", earlier."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OncePerDeadline",
                table: "Notifications",
                columns: new[] { "Kind", "ContentId", "UserId", "DueAt" },
                unique: true,
                filter: "\"Kind\" = 'DeadlineSoon'");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OncePerPublish",
                table: "Notifications",
                columns: new[] { "Kind", "ContentId", "UserId" },
                unique: true,
                filter: "\"Kind\" IN ('NewsPublished', 'AssignmentPublished')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_OncePerDeadline",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OncePerPublish",
                table: "Notifications");
        }
    }
}
