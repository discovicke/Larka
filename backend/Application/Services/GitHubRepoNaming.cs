using System.Text;
using System.Text.RegularExpressions;
using Backend.Domain.Entities;

namespace Backend.Application.Services;

/// <summary>
/// Bygger GitHub-reponamn från ett mönster. Samma kod används av preview-endpointen och provisioning,
/// så läraren ser exakt det namn som skapas. Mönstret får bara innehålla kända platshållare och
/// GitHub-säkra tecken.
/// </summary>
public static partial class GitHubRepoNaming
{
    public const string DefaultPattern = "{assignmentSlug}-{githubLogin}";

    private const int _maxRepoNameLength = 90;

    private static readonly HashSet<string> _placeholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "assignmentTitle",
        "assignmentSlug",
        "githubLogin",
        "studentEmail",
        "studentName",
        "studentId"
    };

    private static readonly HashSet<string> _uniquePlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "githubLogin",
        "studentEmail",
        "studentId"
    };

    public static string EffectivePattern(string? pattern) =>
        string.IsNullOrWhiteSpace(pattern)
            ? DefaultPattern
            : pattern.Trim();

    public static string? Validate(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var effective = pattern.Trim();
        if (effective.Length > 120)
        {
            return "repoNamePattern is too long";
        }

        var found = PlaceholderPattern().Matches(effective)
            .Select(match => match.Groups[1].Value)
            .ToList();
        var unknown = found
            .Where(name => !_placeholders.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (unknown.Count > 0)
        {
            return "repoNamePattern contains unknown placeholders: " + string.Join(", ", unknown);
        }

        if (!found.Any(name => _uniquePlaceholders.Contains(name)))
        {
            return "repoNamePattern must contain {githubLogin}, {studentEmail} or {studentId}";
        }

        var literals = PlaceholderPattern().Replace(effective, "");
        if (literals.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not ('-' or '_' or '.' or ' ')))
        {
            return "repoNamePattern may only contain letters, digits, space, hyphen, underscore, dot and placeholders";
        }

        return null;
    }

    public static string ForStudent(
        string? pattern,
        string assignmentTitle,
        User student,
        HashSet<string>? usedNames = null) =>
        Render(
            pattern,
            new RepoNameValues(
                assignmentTitle,
                student.GitHubLogin,
                student.Name,
                student.Email,
                student.Id),
            usedNames);

    public static string Render(
        string? pattern,
        RepoNameValues values,
        HashSet<string>? usedNames = null)
    {
        var effective = EffectivePattern(pattern);
        var raw = PlaceholderPattern().Replace(effective, match =>
        {
            var name = match.Groups[1].Value;
            return name.ToLowerInvariant() switch
            {
                "assignmenttitle" => values.AssignmentTitle,
                "assignmentslug" => Slug(values.AssignmentTitle, 50, "assignment"),
                "githublogin" => LoginPart(values.GitHubLogin, values.StudentEmail),
                "studentemail" => LoginPart(null, values.StudentEmail),
                "studentname" => values.StudentName,
                "studentid" => values.StudentId.ToString("N")[..8],
                _ => ""
            };
        });

        var name = Slug(raw, _maxRepoNameLength, "assignment");

        // Om det valfria mönstret innehåller en unik platshållare ska den finnas kvar även när
        // en lång titel tvingar oss att korta namnet.
        var uniquePart = UniquePart(effective, values);
        if (uniquePart is not null
            && !name.Contains(uniquePart, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = Slug(name, _maxRepoNameLength - uniquePart.Length - 1, "assignment");
            name = $"{prefix}-{uniquePart}";
        }

        if (usedNames is null || usedNames.Add(name))
        {
            return name;
        }

        var suffix = values.StudentId.ToString("N")[..8];
        var baseName = Slug(name, _maxRepoNameLength - suffix.Length - 1, "assignment");
        var unique = $"{baseName}-{suffix}";
        usedNames.Add(unique);
        return unique;
    }

    // GitHub-reponamn: gemener, siffror och bindestreck. Samma mönster och elev ska ge samma namn.
    private static string Slug(string value, int maxLength, string fallback)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        if (slug.Length == 0)
        {
            slug = fallback;
        }

        return slug.Length > maxLength
            ? slug[..maxLength].TrimEnd('-')
            : slug;
    }

    private static string? UniquePart(string pattern, RepoNameValues values)
    {
        if (pattern.Contains("{githubLogin}", StringComparison.OrdinalIgnoreCase))
        {
            return Slug(LoginPart(values.GitHubLogin, values.StudentEmail), 40, "student");
        }

        if (pattern.Contains("{studentEmail}", StringComparison.OrdinalIgnoreCase))
        {
            return Slug(LoginPart(null, values.StudentEmail), 40, "student");
        }

        if (pattern.Contains("{studentId}", StringComparison.OrdinalIgnoreCase))
        {
            return values.StudentId.ToString("N")[..8];
        }

        return null;
    }

    private static string LoginPart(string? gitHubLogin, string email)
    {
        if (!string.IsNullOrWhiteSpace(gitHubLogin))
        {
            return gitHubLogin;
        }

        return email.Split('@', 2)[0];
    }

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex PlaceholderPattern();
}

public sealed record RepoNameValues(
    string AssignmentTitle,
    string? GitHubLogin,
    string StudentName,
    string StudentEmail,
    Guid StudentId);
