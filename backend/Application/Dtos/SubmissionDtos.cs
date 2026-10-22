namespace Backend.Application.Dtos;

public record SubmissionFileDto(string FileName, string Url, long SizeBytes);

// Files ersätter tidigare filer vid ny inlämning (t.ex. efter komplettering).
public record SubmitDto(string? BodyMd = null, List<SubmissionFileDto>? Files = null);

// NeedsWork = begär komplettering: bara feedback, ingen poäng.
public record GradeDto(int? Score, string Feedback, bool NeedsWork = false);
