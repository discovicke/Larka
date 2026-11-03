using Backend.Application.Abstractions;
using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace backend.Tests;

public class RepoLockPolicyTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static RepoLockContext Context(DateTimeOffset? personalDueAt) =>
        new(
            new StudentRepo(),
            new Submission { Status = SubmissionStatus.NotStarted, PersonalDueAt = personalDueAt },
            Guid.NewGuid(),
            _now.AddDays(-1),
            _now.AddDays(-2),
            [_now.AddDays(-1)],
            personalDueAt);

    [Fact]
    public void RepoWithoutPersonalDate_LocksWhenNoClassDeadlineRemains() =>
        Assert.True(RepoLockPolicy.LocksWithoutOwnCommit(Context(null), _now));

    [Fact]
    public void RepoWithFuturePersonalDate_StaysOpen() =>
        Assert.False(RepoLockPolicy.LocksWithoutOwnCommit(Context(_now.AddDays(1)), _now));

    [Fact]
    public void RepoWithPassedPersonalDate_Locks() =>
        Assert.True(RepoLockPolicy.LocksWithoutOwnCommit(Context(_now.AddHours(-1)), _now));
}
