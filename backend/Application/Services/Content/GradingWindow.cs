using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

// Vad deadlinerna tillåter just nu: när eleven får lämna in och när läraren får sätta IG eller Komplettera.
// Reglerna bor här och ingen annanstans. Klienten räknar inget själv: den visar DeadlineStateDto som backend skickar.
// En uppgift utan DueAt har ingen deadlinelogik: inlämning står öppen tills den bedömts och alla utfall är tillåtna.
public readonly record struct GradingWindow(
    DateTimeOffset? DueAt,
    IReadOnlyList<DateTimeOffset> ResubmitDueAt,
    DateTimeOffset Now,
    DateTimeOffset? PersonalDueAt = null)
{
    public const string ClosedMessage = "Deadlinen har gått ut och det finns ingen kompletteringsdeadline kvar.";
    public const string LockedMessage = "Din inlämning är låst tills läraren har bedömt den.";
    public const string FailTooEarlyMessage =
        "IG kan bara sättas när sista deadline har passerat. Använd Komplettera så länge en deadline återstår.";
    public const string NeedsWorkTooLateMessage =
        "Komplettera kan inte begäras när ingen deadline återstår. Sätt IG eller godkänn inlämningen.";

    /// <summary>Fönstret för en länks deadlines just nu.</summary>
    public static GradingWindow Of(ContentLink link, DateTimeOffset now) => new(link.DueAt, link.ResubmitDueAt, now);

    /// <summary>Fönstret för en elevs inlämning: länkens deadlines plus elevens egen extra deadline.</summary>
    public static GradingWindow Of(ContentLink link, Submission? submission, DateTimeOffset now) =>
        new(link.DueAt, link.ResubmitDueAt, now, submission?.PersonalDueAt);

    /// <summary>Alla deadlines som gäller eleven: DueAt, kompletteringarna och elevens egen. Tom utan DueAt.</summary>
    private IEnumerable<DateTimeOffset> Deadlines()
    {
        if (DueAt is null)
        {
            return [];
        }

        var all = ResubmitDueAt.Prepend(DueAt.Value);
        return PersonalDueAt is null
            ? all
            : all.Append(PersonalDueAt.Value);
    }

    public bool HasDeadline => DueAt is not null;

    /// <summary>Klassens sista deadline (DueAt eller sista kompletteringen), utan elevens egen. Null utan DueAt.</summary>
    public DateTimeOffset? LastClassDeadline => DueAt is null
        ? null
        : ResubmitDueAt.Append(DueAt.Value).Max();

    /// <summary>
    /// Deadlinen som stänger elevens inlämning, eller null när inlämningen är stängd eller uppgiften saknar deadline.
    /// En Inlämnad inlämning får ändras till DueAt. Annars gäller nästa deadline som återstår.
    /// </summary>
    public DateTimeOffset? HandInUntil(SubmissionStatus status)
    {
        if (DueAt is null || HandInError(status) is not null)
        {
            return null;
        }

        return status == SubmissionStatus.Submitted
            ? DueAt
            : NextDeadline;
    }

    /// <summary>Första av elevens deadlines (DueAt, kompletteringarna och den personliga) som ligger efter nu, annars null.</summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            var now = Now.UtcDateTime;
            var upcoming = Deadlines()
                .Where(value => value.UtcDateTime > now)
                .Order()
                .ToList();
            return upcoming.Count > 0
                ? upcoming[0]
                : null;
        }
    }

    /// <summary>Senaste av elevens deadlines (DueAt, kompletteringarna och den personliga) som har passerat (eller är nu), annars null.</summary>
    public DateTimeOffset? LastPassedDeadline
    {
        get
        {
            var now = Now.UtcDateTime;
            var passed = Deadlines()
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

    /// <summary>
    /// Automatiskt IG: uppgiften har en deadline, ingen deadline återstår och eleven har inte lämnat in
    /// (NotStarted) eller inte kompletterat (NeedsWork). Härleds alltid och sparas aldrig. Elevens personliga
    /// deadline räknas med i NextDeadline, så att regeln fortfarande bor på ett ställe.
    /// </summary>
    public bool IsAutoFail(SubmissionStatus status) =>
        HasDeadline
        && !DeadlineRemains
        && status is SubmissionStatus.NotStarted or SubmissionStatus.NeedsWork;

    /// <summary>IG (Graded med Fail) får först sättas när ingen deadline återstår.</summary>
    public bool AllowsFail => DueAt is null || !DeadlineRemains;

    /// <summary>Komplettera ger eleven en ny chans, så det kräver att en deadline återstår.</summary>
    public bool AllowsNeedsWork => DueAt is null || DeadlineRemains;
}
