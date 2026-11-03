using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Slår på row level security för alla tabeller i public-schemat som har det avstängt. Körs vid varje start efter
/// migrationerna, så att en ny tabell eller ett återskapat schema aldrig blir öppet. Supabase lämnar ut public-
/// schemat via sitt REST-API (rollerna anon och authenticated). Utan policies ser de rollerna inga rader.
/// Appen ansluter som tabellernas ägare och påverkas inte, eftersom FORCE inte används.
/// </summary>
public static class RowLevelSecurity
{
    private const string _enableSql =
        """
        DO $$
        DECLARE item record;
        BEGIN
          FOR item IN
            SELECT c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND NOT c.relrowsecurity
          LOOP
            EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', item.relname);
          END LOOP;
        END $$;
        """;

    public static void EnableOnAllTables(AppDb db) => db.Database.ExecuteSqlRaw(_enableSql);
}
