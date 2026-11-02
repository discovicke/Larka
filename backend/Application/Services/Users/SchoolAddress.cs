using System.Globalization;
using System.Text.RegularExpressions;

namespace Backend.Application.Services;

// Vad skolans adress säger om personen: fornamn.efternamn.net25@skola.se ger namnet "Fornamn Efternamn" och klassen
// "net25". Ren logik utan databas. Klienten speglar klasskodsregeln i client/src/lib/classCode.ts.
public static partial class SchoolAddress
{
    /// <summary>
    /// Klasskoden i adressen: sista delen före @ efter en punkt, och bara om den har en siffra, så att ett
    /// efternamn aldrig räknas som klass. "christian.gennari.net25@edu.edugrade.se" ger "net25".
    /// </summary>
    public static string? ClassCode(string email)
    {
        var local = email.Split('@')[0];
        var dot = local.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        var code = NormalizeCode(local[(dot + 1)..]);
        return code.Any(char.IsDigit) && code.Any(char.IsLetter)
            ? code
            : null;
    }

    /// <summary>Utan versaler och tecken: ".NET25" och "NET 25" blir båda "net25".</summary>
    public static string NormalizeCode(string text) => NonAlphanumeric().Replace(text, "").ToLowerInvariant();

    /// <summary>
    /// Namnet ur adressen: delar på punkt och understreck, tar bort klasskoden sist och gör varje del (och varje
    /// del av ett bindestreck) till stor första bokstav. "anna-karin.berg.net25@..." ger "Anna-Karin Berg".
    /// Null när adressen inte ger något namn.
    /// </summary>
    public static string? NameFromEmail(string email)
    {
        var local = email.Split('@')[0].Trim();
        var parts = local.Split(['.', '_'], StringSplitOptions.RemoveEmptyEntries).ToList();
        if (ClassCode(email) is not null)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var words = parts
            .Select(part => string.Join('-', part.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize)))
            .Where(word => word.Length > 0)
            .ToList();
        return words.Count == 0
            ? null
            : string.Join(' ', words);
    }

    /// <summary>
    /// Namnet från ID-tokenet om det finns och inte bara är e-postadressen (så visar Microsoft ett konto utan
    /// visningsnamn), annars namnet ur adressen, och sist själva adressen.
    /// </summary>
    public static string DisplayName(string? tokenName, string email)
    {
        var name = tokenName?.Trim();
        if (!string.IsNullOrEmpty(name) && !name.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return NameFromEmail(email) ?? email.Trim();
    }

    private static string Capitalize(string word) =>
        char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..].ToLowerInvariant();

    [GeneratedRegex("[^\\p{L}\\p{N}]")]
    private static partial Regex NonAlphanumeric();
}
