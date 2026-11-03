using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMentorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mentorrollen finns inte längre. Rollen lagras som text, så ingen kolumn eller tabell ändras: bara
            // medlemskapen med rollen Mentor tas bort. Inlämningar hör till användaren och berörs inte.
            migrationBuilder.Sql("DELETE FROM \"Enrollments\" WHERE \"Role\" = 'Mentor';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Inget schema ändrades, och de borttagna mentormedlemskapen går inte att få tillbaka.
        }
    }
}
