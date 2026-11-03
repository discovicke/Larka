using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNotSubmittedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ej inlämnat sparas inte längre, det härleds som automatiskt IG. Statusen lagras som text.
            // Utan inlämning blir raden NotStarted, annars NeedsWork. Gamla betygsrader ligger kvar.
            migrationBuilder.Sql(
                """
                UPDATE "Submissions"
                SET "Status" = CASE WHEN "SubmittedAt" IS NULL THEN 'NotStarted' ELSE 'NeedsWork' END
                WHERE "Status" = 'NotSubmitted';
                """);

            // Notistypen är borttagen. Notiser lagras som text. Avstängda typer lagras som heltal (typen hade värde 6).
            migrationBuilder.Sql("""DELETE FROM "Notifications" WHERE "Kind" = 'SubmissionNotSubmitted';""");
            migrationBuilder.Sql(
                """UPDATE "Users" SET "MutedNotificationKinds" = array_remove("MutedNotificationKinds", 6);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dataomvandlingen går inte att ångra: raderna kan inte skiljas från riktiga NotStarted/NeedsWork.
        }
    }
}
