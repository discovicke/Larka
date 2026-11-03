using Backend.Application.Services;
using Backend.Domain.Enums;

namespace backend.Tests;

public class GradingWindowTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static GradingWindow Window(DateTimeOffset? dueAt, params DateTimeOffset[] resubmitDueAt) =>
        new(dueAt, resubmitDueAt, _now);

    [Theory]
    [InlineData(SubmissionStatus.NotStarted, true)]
    [InlineData(SubmissionStatus.NeedsWork, true)]
    [InlineData(SubmissionStatus.Submitted, false)]
    [InlineData(SubmissionStatus.Graded, false)]
    public void IsAutoFail_AfterTheLastDeadline_OnlyForMissingWork(SubmissionStatus status, bool expected)
    {
        var window = Window(_now.AddDays(-5), _now.AddDays(-1));

        Assert.Equal(expected, window.IsAutoFail(status));
    }

    [Fact]
    public void IsAutoFail_IncludesTheMomentTheLastDeadlinePasses()
    {
        Assert.True(Window(_now).IsAutoFail(SubmissionStatus.NotStarted));
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void IsAutoFail_IsFalse_WhileAResubmitDeadlineRemains(SubmissionStatus status)
    {
        Assert.False(Window(_now.AddDays(-5), _now.AddDays(3)).IsAutoFail(status));
    }

    [Fact]
    public void IsAutoFail_IsFalse_BeforeTheMainDeadline()
    {
        Assert.False(Window(_now.AddDays(1)).IsAutoFail(SubmissionStatus.NotStarted));
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void IsAutoFail_IsFalse_WithoutADueDate(SubmissionStatus status)
    {
        Assert.False(Window(null).IsAutoFail(status));
    }

    private static GradingWindow WithPersonal(DateTimeOffset personalDueAt, DateTimeOffset dueAt) =>
        new(dueAt, [], _now, personalDueAt);

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void IsAutoFail_IsFalse_WhileAPersonalDeadlineRemains(SubmissionStatus status)
    {
        Assert.False(WithPersonal(_now.AddDays(2), _now.AddDays(-5)).IsAutoFail(status));
    }

    [Fact]
    public void IsAutoFail_ReturnsAfterThePersonalDeadlinePasses()
    {
        Assert.True(WithPersonal(_now.AddDays(-1), _now.AddDays(-5)).IsAutoFail(SubmissionStatus.NotStarted));
    }

    [Fact]
    public void PersonalDeadline_JoinsTheOtherDeadlines()
    {
        var window = new GradingWindow(_now.AddDays(-5), [_now.AddDays(5)], _now, _now.AddDays(2));

        Assert.Equal(_now.AddDays(2), window.NextDeadline);
        Assert.Equal(_now.AddDays(-5), window.LastPassedDeadline);
        Assert.True(window.AllowsNeedsWork);
        Assert.False(window.AllowsFail);
    }

    [Fact]
    public void PersonalDeadline_EarlierThanTheOthers_ChangesNothing()
    {
        var withPersonal = new GradingWindow(_now.AddDays(-5), [_now.AddDays(5)], _now, _now.AddDays(-2));
        var without = Window(_now.AddDays(-5), _now.AddDays(5));

        Assert.Equal(without.NextDeadline, withPersonal.NextDeadline);
        Assert.Equal(without.DeadlineRemains, withPersonal.DeadlineRemains);
        Assert.Equal(_now.AddDays(-2), withPersonal.LastPassedDeadline);
    }

    [Fact]
    public void PersonalDeadline_WithoutADueDate_IsIgnored()
    {
        var window = new GradingWindow(null, [], _now, _now.AddDays(2));

        Assert.Null(window.NextDeadline);
        Assert.False(window.IsAutoFail(SubmissionStatus.NotStarted));
    }

    [Fact]
    public void HandInError_SubmittedAfterDueAt_StaysLockedDespiteAPersonalDeadline()
    {
        var window = WithPersonal(_now.AddDays(2), _now.AddDays(-5));

        Assert.Equal(GradingWindow.LockedMessage, window.HandInError(SubmissionStatus.Submitted));
        Assert.Null(window.HandInError(SubmissionStatus.NeedsWork));
    }
}
