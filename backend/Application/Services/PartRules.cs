using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>
/// När en del räknas som klar för eleven. Regeln delas av elevvyn och Idag-sidan, så att de visar samma sak.
/// </summary>
public static class PartRules
{
    public static bool IsSatisfied(Content content, PartCompletion? completion, Submission? submission)
    {
        if (IsManual(content))
        {
            return completion is not null;
        }

        if (content.Type != ContentType.Assignment || content.Assignment is null)
        {
            return false;
        }

        if (content.Assignment.CompletionRule == CompletionRule.Approved)
        {
            var level = submission?.Grade?.Level;
            return submission?.Status == SubmissionStatus.Graded
                && (level == GradeLevel.Pass || level == GradeLevel.Distinction);
        }

        // Inlämnat räcker. Ett betyg (t.ex. bedömning på plats) räknas också, även om SubmittedAt saknas.
        return submission?.SubmittedAt is not null || submission?.Status == SubmissionStatus.Graded;
    }

    public static bool IsManual(Content content) =>
        content.Type == ContentType.Theory
        || (content.Type == ContentType.Assignment
            && (content.Assignment is null || content.Assignment.SubmissionModes.Count == 0));
}
