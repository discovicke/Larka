using System.Text.RegularExpressions;

namespace Backend.Application.Services;

// En omnämning sparas i kommentarens text som @[Namn](id). Namnet är det som visas, id:t säger vem som får
// notisen. Servern litar bara på id:t, och bara om personen får läsa innehållet.
public static partial class Mentions
{
    [GeneratedRegex(
        @"@\[([^\]\r\n]{1,200})\]\(([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex Token();

    /// <summary>Id:n som nämns i texten, var och ett en gång.</summary>
    public static HashSet<Guid> UserIdsIn(string? text) =>
        string.IsNullOrEmpty(text)
            ? []
            : Token().Matches(text).Select(match => Guid.Parse(match.Groups[2].Value)).ToHashSet();
}
