using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Services;

/// <summary>
/// När en del räknas som klar för eleven. Regeln delas av elevvyn och Idag-sidan, så att de visar samma sak.
/// </summary>
public static class PartRules
{
    public static bool IsSatisfied(
        Content content, PartCompletion? completion, Submission? submission, GradingWindow? window = null)
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

        // Ett automatiskt IG räknas som ett bedömt IG: eleven kan inte göra mer, och nästa del låses upp.
        if (window?.IsAutoFail(submission?.Status ?? SubmissionStatus.NotStarted) == true)
        {
            return true;
        }

        // Inlämnat räcker. Ett betyg (t.ex. bedömning på plats) räknas också, även om SubmittedAt saknas.
        // En inlämning som läraren begärt komplettering på räknas inte som klar förrän eleven lämnat in igen.
        return submission?.Status == SubmissionStatus.Graded
            || (submission?.SubmittedAt is not null && submission.Status != SubmissionStatus.NeedsWork);
    }

    public static bool IsManual(Content content) =>
        content.Type == ContentType.Theory
        || (content.Type == ContentType.Assignment
            && (content.Assignment is null || content.Assignment.SubmissionModes.Count == 0));
}
