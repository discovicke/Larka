namespace Backend.Application.Services;

// Kompletteringsdeadlines: extra tillfällen att lämna in igen efter ordinarie deadline.
public static class ResubmitDeadlines
{
    public const int MaxCount = 5;

    /// <summary>Felmeddelande om listan är ogiltig, annars null.</summary>
    public static string? Validate(DateTimeOffset? dueAt, IReadOnlyCollection<DateTimeOffset>? resubmitDueAt)
    {
        if (resubmitDueAt is null || resubmitDueAt.Count == 0)
        {
            return null;
        }

        if (dueAt is null)
        {
            return "resubmitDueAt requires dueAt";
        }

        if (resubmitDueAt.Count > MaxCount)
        {
            return $"at most {MaxCount} resubmitDueAt values are allowed";
        }

        if (resubmitDueAt.Any(value => value <= dueAt))
        {
            return "resubmitDueAt must be after dueAt";
        }

        return resubmitDueAt.Distinct().Count() != resubmitDueAt.Count
            ? "resubmitDueAt must not contain duplicates"
            : null;
    }

    /// <summary>Sparas i stigande ordning.</summary>
    public static List<DateTimeOffset> Sorted(IEnumerable<DateTimeOffset>? resubmitDueAt) =>
        resubmitDueAt is null
            ? []
            : resubmitDueAt.Order().ToList();
}
