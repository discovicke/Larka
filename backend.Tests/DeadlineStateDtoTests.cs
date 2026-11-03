using Backend.Application.Dtos;
using Backend.Application.Services;
using Backend.Domain.Enums;

namespace backend.Tests;

public class DeadlineStateDtoTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static DeadlineStateDto StateOf(
        DateTimeOffset? dueAt,
        SubmissionStatus status,
        DateTimeOffset? personalDueAt = null,
        params DateTimeOffset[] resubmitDueAt) =>
        DeadlineStateDto.From(new GradingWindow(dueAt, resubmitDueAt, _now, personalDueAt), status);

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    [InlineData(SubmissionStatus.Submitted)]
    public void WithoutADueDate_TheHandInIsOpenWithoutAnEndAndNeverAutoFails(SubmissionStatus status)
    {
        var state = StateOf(null, status);

        Assert.True(state.CanHandIn);
        Assert.Null(state.HandInUntil);
        Assert.Null(state.HandInClosed);
        Assert.Null(state.NextDeadline);
        Assert.Null(state.LastClassDeadline);
        Assert.False(state.AutoFail);
        Assert.True(state.AllowsFail);
        Assert.True(state.AllowsNeedsWork);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void BeforeTheDueDate_TheHandInIsOpenUntilTheDueDate(SubmissionStatus status)
    {
        var dueAt = _now.AddDays(2);

        var state = StateOf(dueAt, status);

        Assert.True(state.CanHandIn);
        Assert.Equal(dueAt, state.HandInUntil);
        Assert.Equal(dueAt, state.NextDeadline);
        Assert.Null(state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Fact]
    public void BeforeTheDueDate_ASubmittedHandInCanChangeUntilTheDueDate()
    {
        var dueAt = _now.AddDays(2);

        var state = StateOf(dueAt, SubmissionStatus.Submitted, null, _now.AddDays(10));

        Assert.True(state.CanHandIn);
        Assert.Equal(dueAt, state.HandInUntil);
        Assert.Null(state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void AfterTheDueDate_WithAResubmitDeadline_TheHandInStaysOpenUntilIt(SubmissionStatus status)
    {
        var resubmitDueAt = _now.AddDays(5);

        var state = StateOf(_now.AddDays(-2), status, null, resubmitDueAt);

        Assert.True(state.CanHandIn);
        Assert.Equal(resubmitDueAt, state.HandInUntil);
        Assert.Equal(resubmitDueAt, state.NextDeadline);
        Assert.Null(state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Fact]
    public void AfterTheDueDate_ASubmittedHandInIsLockedEvenWithAResubmitDeadline()
    {
        var state = StateOf(_now.AddDays(-2), SubmissionStatus.Submitted, null, _now.AddDays(5));

        Assert.False(state.CanHandIn);
        Assert.Null(state.HandInUntil);
        Assert.Equal(HandInClosedReason.Locked, state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void AfterTheLastDeadline_MissingWorkIsClosedAndFailsAutomatically(SubmissionStatus status)
    {
        var state = StateOf(_now.AddDays(-5), status, null, _now.AddDays(-1));

        Assert.False(state.CanHandIn);
        Assert.Null(state.HandInUntil);
        Assert.Null(state.NextDeadline);
        Assert.Equal(HandInClosedReason.Closed, state.HandInClosed);
        Assert.True(state.AutoFail);
    }

    [Fact]
    public void AfterTheLastDeadline_ASubmittedHandInIsLockedAndDoesNotFailAutomatically()
    {
        var state = StateOf(_now.AddDays(-5), SubmissionStatus.Submitted, null, _now.AddDays(-1));

        Assert.False(state.CanHandIn);
        Assert.Equal(HandInClosedReason.Locked, state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void APersonalDeadline_ExtendsTheHandInAndRemovesTheAutomaticFail(SubmissionStatus status)
    {
        var personalDueAt = _now.AddDays(7);

        var state = StateOf(_now.AddDays(-5), status, personalDueAt);

        Assert.True(state.CanHandIn);
        Assert.Equal(personalDueAt, state.HandInUntil);
        Assert.Equal(personalDueAt, state.NextDeadline);
        Assert.Null(state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Fact]
    public void AGradedHandIn_CannotBeHandedInAndIsNotClosedForAReason()
    {
        var state = StateOf(_now.AddDays(-5), SubmissionStatus.Graded, null, _now.AddDays(-1));

        Assert.False(state.CanHandIn);
        Assert.Null(state.HandInUntil);
        Assert.Null(state.HandInClosed);
        Assert.False(state.AutoFail);
    }

    [Fact]
    public void AGradedHandIn_BeforeTheDueDate_IsStillNotOpen()
    {
        var state = StateOf(_now.AddDays(3), SubmissionStatus.Graded);

        Assert.False(state.CanHandIn);
        Assert.Null(state.HandInUntil);
        Assert.Null(state.HandInClosed);
    }

    [Fact]
    public void FailIsAllowedOnlyAfterTheLastDeadline()
    {
        var before = StateOf(_now.AddDays(-5), SubmissionStatus.Submitted, null, _now.AddDays(1));
        var after = StateOf(_now.AddDays(-5), SubmissionStatus.Submitted, null, _now.AddDays(-1));

        Assert.False(before.AllowsFail);
        Assert.True(before.AllowsNeedsWork);
        Assert.True(after.AllowsFail);
        Assert.False(after.AllowsNeedsWork);
    }

    [Fact]
    public void ThePersonalDeadline_KeepsNeedsWorkAllowedAndFailBlockedUntilItPasses()
    {
        var state = StateOf(_now.AddDays(-5), SubmissionStatus.Submitted, _now.AddDays(3));

        Assert.True(state.AllowsNeedsWork);
        Assert.False(state.AllowsFail);
    }

    [Fact]
    public void TheLastClassDeadline_IgnoresThePersonalDate()
    {
        var lastResubmit = _now.AddDays(-1);

        var state = StateOf(_now.AddDays(-5), SubmissionStatus.NotStarted, _now.AddDays(30), lastResubmit);

        Assert.Equal(lastResubmit, state.LastClassDeadline);
    }
}
