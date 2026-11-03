using Backend.Application.Services;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

/// <summary>Varför eleven inte får lämna in: inlämningen väntar på bedömning, eller ingen deadline återstår.</summary>
public enum HandInClosedReason
{
    Locked,
    Closed
}

/// <summary>
/// Vad deadlinerna tillåter för en elevs inlämning just nu, uträknat av <see cref="GradingWindow"/>. Klienten visar
/// det och räknar inget själv, så att reglerna bara finns på ett ställe.
/// </summary>
public record DeadlineStateDto(
    DateTimeOffset? NextDeadline, // nästa av elevens deadlines (huvud, komplettering och egen) som återstår
    DateTimeOffset? LastClassDeadline, // klassens sista deadline, utan elevens egen
    bool AutoFail, // automatiskt IG: ingen deadline återstår och eleven har inte lämnat in eller kompletterat
    bool CanHandIn,
    DateTimeOffset? HandInUntil, // till när eleven får lämna in eller ändra, null utan deadline eller när det är stängt
    HandInClosedReason? HandInClosed,
    bool AllowsFail, // läraren får sätta IG
    bool AllowsNeedsWork) // läraren får begära komplettering
{
    public static DeadlineStateDto From(GradingWindow window, SubmissionStatus status)
    {
        var handInError = status == SubmissionStatus.Graded
            ? null
            : window.HandInError(status);
        var closed = handInError switch
        {
            null => (HandInClosedReason?)null,
            GradingWindow.LockedMessage => HandInClosedReason.Locked,
            _ => HandInClosedReason.Closed
        };
        return new DeadlineStateDto(
            window.NextDeadline,
            window.LastClassDeadline,
            window.IsAutoFail(status),
            status != SubmissionStatus.Graded && handInError is null,
            status == SubmissionStatus.Graded
                ? null
                : window.HandInUntil(status),
            closed,
            window.AllowsFail,
            window.AllowsNeedsWork);
    }
}
