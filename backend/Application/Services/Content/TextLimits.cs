namespace Backend.Application.Services;

/// <summary>
/// Hur långa texterna får vara. Gränserna skyddar sidan: en text på en miljon tecken låser fliken för alla som
/// öppnar den. Klienten visar samma gränser innan texten skickas (client/src/lib/textLimits.ts).
/// </summary>
public static class TextLimits
{
    public const int Title = 200;
    public const int ContentBody = 100_000;
    public const int Comment = 5_000;
    public const int SubmissionText = 50_000;
    public const int Feedback = 20_000;
    public const int CourseDescription = 20_000;

    public const string TitleTooLong = "title can be at most 200 characters";
    public const string ContentBodyTooLong = "the text can be at most 100000 characters";
    public const string CommentTooLong = "a comment can be at most 5000 characters";
    public const string SubmissionTextTooLong = "the answer can be at most 50000 characters";
    public const string FeedbackTooLong = "feedback can be at most 20000 characters";
    public const string CourseDescriptionTooLong = "description can be at most 20000 characters";

    public static bool IsTooLong(string? text, int max) => text is not null && text.Length > max;
}
