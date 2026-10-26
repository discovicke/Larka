using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContentModelForCreateForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradeScale",
                table: "AssignmentSpecs",
                type: "text",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "AssignmentSpecs",
                type: "text",
                nullable: false,
                defaultValue: "Lab");

            migrationBuilder.AddColumn<string[]>(
                name: "SubmissionModes",
                table: "AssignmentSpecs",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Grades",
                type: "text",
                nullable: true);

            // Befintliga uppgifter med poäng får skalan IG-G-VG, och poängen blir betyg efter hur nära maxpoängen de är:
            // minst 80 % VG, minst 50 % G, annars IG.
            migrationBuilder.Sql("""
                UPDATE "Grades" g
                SET "Level" = CASE
                    WHEN g."Score" IS NULL THEN NULL
                    WHEN g."Score" * 10 >= a."MaxScore" * 8 THEN 'Distinction'
                    WHEN g."Score" * 2 >= a."MaxScore" THEN 'Pass'
                    ELSE 'Fail'
                END
                FROM "Submissions" s
                JOIN "ContentLinks" l ON l."Id" = s."ContentLinkId"
                JOIN "AssignmentSpecs" a ON a."ContentId" = l."ContentId"
                WHERE g."SubmissionId" = s."Id" AND a."MaxScore" IS NOT NULL;
                """);
            migrationBuilder.Sql("""
                UPDATE "AssignmentSpecs" SET "GradeScale" = 'PassFailDistinction' WHERE "MaxScore" IS NOT NULL;
                """);

            // Ett läge blir en lista med ett eller två sätt: FileOrRepo blir File och Repo, None blir en tom lista.
            migrationBuilder.Sql("""
                UPDATE "AssignmentSpecs"
                SET "SubmissionModes" = CASE "SubmissionMode"
                    WHEN 'File' THEN ARRAY['File']
                    WHEN 'Repo' THEN ARRAY['Repo']
                    WHEN 'FileOrRepo' THEN ARRAY['File', 'Repo']
                    ELSE ARRAY[]::text[]
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "SubmissionMode",
                table: "AssignmentSpecs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "Grades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxScore",
                table: "AssignmentSpecs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionMode",
                table: "AssignmentSpecs",
                type: "text",
                nullable: false,
                defaultValue: "None");

            // Gamla läget kan bara File, Repo eller FileOrRepo. Text och OnSite försvinner.
            migrationBuilder.Sql("""
                UPDATE "AssignmentSpecs"
                SET "SubmissionMode" = CASE
                    WHEN 'Repo' = ANY ("SubmissionModes") AND 'File' = ANY ("SubmissionModes") THEN 'FileOrRepo'
                    WHEN 'Repo' = ANY ("SubmissionModes") THEN 'Repo'
                    WHEN 'File' = ANY ("SubmissionModes") THEN 'File'
                    ELSE 'None'
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "GradeScale",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AssignmentSpecs");

            migrationBuilder.DropColumn(
                name: "SubmissionModes",
                table: "AssignmentSpecs");
        }
    }
}
