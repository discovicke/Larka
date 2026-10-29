using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubRepoAssignmentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowLate",
                table: "AssignmentSpecs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FeedbackPullRequest",
                table: "AssignmentSpecs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockAtDeadline",
                table: "AssignmentSpecs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "RepoNamePattern",
                table: "AssignmentSpecs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkMode",
                table: "AssignmentSpecs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowLate",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "FeedbackPullRequest",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "LockAtDeadline",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "RepoNamePattern",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "WorkMode",
                table: "AssignmentSpecs");
        }
    }
}
