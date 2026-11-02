using System.Text.RegularExpressions;

namespace Backend.Application.Services;

// En enda regel för e-postadresser. Den är densamma som klientens förhandsvisning av inklistrade elevlistor
// (client/src/lib/importStudents.ts) och formuläret Skapa användare: något före och efter @, och en punkt i domänen.
public static partial class EmailRule
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? email) => email is not null && Pattern().IsMatch(email);
}
