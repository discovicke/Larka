using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Vad deadlinerna tillåter just nu: när eleven får lämna in och när läraren får sätta IG eller Komplettera.
// Reglerna bor här och ingen annanstans. Klientens spegling finns i client/src/lib/gradingWindow.ts.
// En uppgift utan DueAt har ingen deadlinelogik: inlämning står öppen tills den bedömts och alla utfall är tillåtna.
public readonly record struct GradingWindow(
    DateTimeOffset? DueAt,
    IReadOnlyList<DateTimeOffset> ResubmitDueAt,
    DateTimeOffset Now)
{
    public const string ClosedMessage = "Deadlinen har gått ut och det finns ingen kompletteringsdeadline kvar.";
    public const string LockedMessage = "Deadlinen har gått ut. Din inlämning är låst tills läraren har bedömt den.";
    public const string FailTooEarlyMessage =
        "IG kan bara sättas när sista deadline har passerat. Använd Komplettera eller Ej inlämnat så länge en deadline återstår.";
    public const string NeedsWorkTooLateMessage =
        "Komplettera kan inte begäras när ingen deadline återstår. Sätt IG eller godkänn inlämningen.";

    public const string NoDeadlineToMarkMessage =
        "Uppgiften har ingen deadline, så ingen kan markeras som ej inlämnad.";
    public const string DeadlineNotPassedMessage =
        "Deadlinen har inte gått ut ännu. Markera som ej inlämnat först efter deadlinen.";

    public bool HasDeadline => DueAt is not null;

    /// <summary>Första av DueAt och kompletteringsdeadlinerna som ligger efter nu, annars null.</summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            if (DueAt is null)
            {
                return null;
            }

            var now = Now.UtcDateTime;
            var upcoming = ResubmitDueAt
                .Prepend(DueAt.Value)
                .Where(value => value.UtcDateTime > now)
                .Order()
                .ToList();
            return upcoming.Count > 0
                ? upcoming[0]
                : null;
        }
    }

    /// <summary>Senaste av DueAt och kompletteringsdeadlinerna som har passerat (eller är nu), annars null.</summary>
    public DateTimeOffset? LastPassedDeadline
    {
        get
        {
            if (DueAt is null)
            {
                return null;
            }

            var now = Now.UtcDateTime;
            var passed = ResubmitDueAt
                .Prepend(DueAt.Value)
                .Where(value => value.UtcDateTime <= now)
                .OrderDescending()
                .ToList();
            return passed.Count > 0
                ? passed[0]
                : null;
        }
    }

    /// <summary>Sant om någon deadline återstår. Utan DueAt finns ingen deadline att vänta på.</summary>
    public bool DeadlineRemains => NextDeadline is not null;

    /// <summary>Felmeddelande om eleven inte får lämna in med den här statusen nu, annars null.</summary>
    public string? HandInError(SubmissionStatus status)
    {
        if (DueAt is null)
        {
            return null;
        }

        if (status == SubmissionStatus.Submitted)
        {
            return Now.UtcDateTime < DueAt.Value.UtcDateTime
                ? null
                : LockedMessage;
        }

        return DeadlineRemains
            ? null
            : ClosedMessage;
    }

    /// <summary>IG (Graded med Fail) får först sättas när ingen deadline återstår.</summary>
    public bool AllowsFail => DueAt is null || !DeadlineRemains;

    /// <summary>Komplettera ger eleven en ny chans, så det kräver att en deadline återstår.</summary>
    public bool AllowsNeedsWork => DueAt is null || DeadlineRemains;
}
