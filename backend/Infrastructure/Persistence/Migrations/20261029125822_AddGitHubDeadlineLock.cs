using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubDeadlineLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockError",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionSha",
                table: "StudentRepos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockError",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "SubmissionSha",
                table: "StudentRepos");
        }
    }
}
