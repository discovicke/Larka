using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMutedNotificationKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "MutedNotificationKinds",
                table: "Users",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "MutedNotificationKinds",
                value: new int[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MutedNotificationKinds",
                table: "Users");
        }
    }
}
