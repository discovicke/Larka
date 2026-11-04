using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "Notifications",
                type: "timestamp with time zone",
                nullable: true);

            // Påminnelser gäller en deadline var (#511). De som redan skickats gällde huvuddeadlinen: första
            // huvuddeadlinen för innehållet efter att notisen skapades, så att eleven inte påminns om den igen.
            migrationBuilder.Sql(
                """
                UPDATE "Notifications" AS notification
                SET "DueAt" = (
                    SELECT MIN(link."DueAt") FROM "ContentLinks" AS link
                    WHERE link."ContentId" = notification."ContentId" AND link."DueAt" > notification."CreatedAt")
                WHERE notification."Kind" = 'DeadlineSoon' AND notification."DueAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Notifications");
        }
    }
}
