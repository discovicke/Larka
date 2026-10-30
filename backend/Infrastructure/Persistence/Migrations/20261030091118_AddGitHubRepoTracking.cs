using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubRepoTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChecksPassed",
                table: "StudentRepos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ChecksTotal",
                table: "StudentRepos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommitCount",
                table: "StudentRepos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastCommitAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastCommitMessage",
                table: "StudentRepos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChecksPassed",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "ChecksTotal",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "CommitCount",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "LastCommitAt",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "LastCommitMessage",
                table: "StudentRepos");
        }
    }
}
