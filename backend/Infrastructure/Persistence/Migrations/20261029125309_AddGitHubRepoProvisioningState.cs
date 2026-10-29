using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubRepoProvisioningState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentTitle",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesiredRepoName",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextProvisioningAttemptAt",
                table: "StudentRepos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvisioningAttempts",
                table: "StudentRepos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningError",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvisioningState",
                table: "StudentRepos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RepoNamePattern",
                table: "StudentRepos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateRepo",
                table: "StudentRepos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentTitle",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "DesiredRepoName",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "NextProvisioningAttemptAt",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "ProvisioningAttempts",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "ProvisioningError",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "ProvisioningState",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "RepoNamePattern",
                table: "StudentRepos");

            migrationBuilder.DropColumn(
                name: "TemplateRepo",
                table: "StudentRepos");
        }
    }
}
