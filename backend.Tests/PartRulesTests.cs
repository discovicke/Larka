using Backend.Application.Services;
using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace backend.Tests;

public class PartRulesTests
{
    private static readonly DateTimeOffset _submittedAt = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static Content Assignment(CompletionRule rule) =>
        new()
        {
            Type = ContentType.Assignment,
            Assignment = new AssignmentSpec
            {
                CompletionRule = rule,
                SubmissionModes = [SubmissionMode.Text]
            }
        };

    [Theory]
    [InlineData(SubmissionStatus.NotStarted, false, false)]
    [InlineData(SubmissionStatus.NeedsWork, true, false)]
    [InlineData(SubmissionStatus.Submitted, true, true)]
    [InlineData(SubmissionStatus.Graded, true, true)]
    [InlineData(SubmissionStatus.Graded, false, true)]
    public void IsSatisfied_WhenHandedIn_IgnoresNeedsWork(SubmissionStatus status, bool hasSubmittedAt, bool expected)
    {
        var submission = new Submission
        {
            Status = status,
            SubmittedAt = hasSubmittedAt
                ? _submittedAt
                : null
        };

        Assert.Equal(expected, PartRules.IsSatisfied(Assignment(CompletionRule.Submitted), null, submission));
    }

    [Theory]
    [InlineData(SubmissionStatus.NotStarted)]
    [InlineData(SubmissionStatus.NeedsWork)]
    public void IsSatisfied_AutomaticFail_CountsAsComplete_OnlyWhenNoDeadlineRemains(SubmissionStatus status)
    {
        var now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var submission = new Submission { Status = status };
        var closed = new GradingWindow(now.AddDays(-2), [], now);
        var open = new GradingWindow(now.AddDays(-2), [now.AddDays(2)], now);
        var content = Assignment(CompletionRule.Submitted);

        Assert.True(PartRules.IsSatisfied(content, null, submission, closed));
        Assert.False(PartRules.IsSatisfied(content, null, submission, open));
        Assert.False(PartRules.IsSatisfied(content, null, null, null));
    }
}
