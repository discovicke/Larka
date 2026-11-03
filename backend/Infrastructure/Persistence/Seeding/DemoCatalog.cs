using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Hur elevernas inlämningar på en uppgift ser ut. Mönstret avgör fördelningen mellan väntar på bedömning,
/// komplettering och betygsatt, så att varje läge finns att visa.
/// </summary>
internal enum SubmissionPattern
{
    None, // inga inlämningar än (uppgiften ligger framåt i tiden)
    Finished, // alla elever är betygsatta (avslutad kurs)
    MostlyGraded, // nästan alla har lämnat in och är betygsatta, en ska komplettera, någon saknas
    Queue, // blandat: många väntar på bedömning, några betygsatta, några ska komplettera, några saknas
    Early // deadlinen har gått eller är nära, bara några har lämnat in och väntar på bedömning
}

// Was är den tidigare titeln, om delen, veckan eller nyheten döpts om. DemoContentRefresh använder den för att hitta
// raden i en databas som seedats med den gamla titeln. Sätt den till den gamla titeln exakt, och byt aldrig titel utan.
internal sealed record PartSpec(
    string Title,
    string Body,
    ContentType Type,
    DateTimeOffset? DueAt,
    GradeScale Scale,
    SubmissionMode[] Modes,
    SubmissionPattern Pattern,
    string[] Answers,
    string[] Feedback,
    string[] NeedsWork,
    string? Was = null);

internal sealed record ModuleSpec(
    string Title,
    string Description,
    DateTimeOffset? PublishAt,
    int Completion,
    PartSpec[] Parts,
    bool Sequential = false,
    string? Was = null);

internal sealed record CommentSpec(string Author, string Body, int HoursAfter);

internal sealed record NewsSpec(
    string Title,
    string Body,
    DateTimeOffset At,
    string Author,
    bool Pinned,
    bool InCourse,
    CommentSpec[] Comments,
    string? Was = null);

internal sealed record CourseSpec(
    string Code,
    string Title,
    string Description,
    int Points,
    DateOnly Start,
    DateOnly End,
    string Author,
    ModuleSpec[] Modules,
    NewsSpec[] News)
{
    /// <summary>
    /// Nycklarna för kursens lärare (CourseTeacher), som i DemoPeople plus larare. Läraren behöver inte vara med i
    /// klassen. Författaren ska vara en av dem. DemoContentRefresh gör prod lik detta.
    /// </summary>
    public string[] Teachers { get; init; } = [];
}

internal sealed record ClassSpec(
    string Name,
    string Term,
    GroupType Type,
    string[] Teachers,
    int FirstStudent,
    int StudentCount,
    bool HasPublicStudent,
    CourseSpec[] Courses,
    NewsSpec[] News);

/// <summary>
/// Själva demoinnehållet: klasser, kurser, veckor, lektioner, uppgifter och nyheter. Datumen är fasta
/// (hösten 2026, demon är måndagen 2026-11-02), så att något är försenat, något ska in i veckan och något ligger längre fram.
/// </summary>
internal static partial class DemoCatalog
{
    private static readonly SubmissionMode[] _text = [SubmissionMode.Text];
    private static readonly SubmissionMode[] _onSite = [SubmissionMode.OnSite];
    private static readonly SubmissionMode[] _none = [];
    private static readonly string[] _noText = [];

    private static readonly string[] _passFeedback =
    [
        "Bra jobbat, lösningen uppfyller kraven.",
        "Godkänd. Tydlig redovisning, men fundera på hur du hade kunnat testa mer.",
        "Fungerar som den ska. Håll koll på namngivningen så blir koden lättare att läsa.",
        "Bra struktur och rimliga val. Godkänd."
    ];

    private static readonly string[] _distinctionFeedback =
    [
        "Mycket bra. Välmotiverade val och en redovisning som visar att du förstått varför, inte bara hur.",
        "Väl genomfört, och du går längre än kraven. Det lyfter till VG.",
        "Utmärkt arbete, särskilt felhanteringen och dokumentationen."
    ];

    private static readonly string[] _needsWorkFeedback =
    [
        "Du är nästan i mål, men en del saknas. Komplettera enligt kommentarerna och lämna in igen.",
        "Redovisningen är för kort för att jag ska kunna bedöma den. Utveckla resonemanget och lämna in på nytt.",
        "Idén är rätt, men lösningen fungerar inte fullt ut än. Rätta felen vi pratade om och försök igen."
    ];

    private static readonly string[] _notSubmittedFeedback =
    [
        "Jag har inte fått någon inlämning av dig. Hör av dig om du vill ha hjälp att komma igång.",
        "Uppgiften lämnades aldrig in. Prata med mig om du behöver mer tid, så hittar vi en lösning."
    ];

    private static readonly string[] _genericAnswers =
    [
        "Jag har löst uppgiften enligt instruktionerna och redovisar lösningen i repot som vi delat med dig. "
        + "Det svåraste var att få ordning på felhanteringen, men det löste sig efter att jag skrivit tester.",
        "Här är min lösning. Jag valde en enkel struktur och höll mig till det vi gick igenom på lektionen. "
        + "Jag har kört alla exempel och de ger förväntat resultat.",
        "Klart. Jag arbetade tillsammans med en kurskamrat och vi delade upp uppgiften i delar som vi sedan "
        + "satte ihop. Vi har dokumenterat hur man kör lösningen i README."
    ];

    public static ClassSpec[] Classes() =>
    [
        Net25(),
        Net24(),
        Fiber25(),
        Committee()
    ];

    // --- Byggstenar ---

    private static ModuleSpec Week(
        string title, string description, DateOnly start, PartSpec[] parts, string? was = null) =>
        new(title, description, Se(start, 7), 92, parts, Was: was);

    private static PartSpec Lesson(string title, string body, string? was = null) =>
        new(
            title,
            body,
            ContentType.Theory,
            null,
            GradeScale.None,
            _none,
            SubmissionPattern.None,
            _noText,
            _noText,
            _noText,
            was);

    /// <summary>En uppgift utan inlämning. Eleven klarmarkerar den själv.</summary>
    private static PartSpec Reading(string title, string body, DateTimeOffset due, string? was = null) =>
        new(
            title,
            body,
            ContentType.Assignment,
            due,
            GradeScale.None,
            _none,
            SubmissionPattern.None,
            _noText,
            _noText,
            _noText,
            was);

    /// <summary>
    /// En övningsuppgift ("Övningsuppgift: …"): ingen skala, inget betyg och ingen deadline, och inga inlämningar
    /// i demodatan. Med modes kan eleven ändå lämna in (t.ex. _text), men seeden lägger aldrig in några svar.
    /// </summary>
    private static PartSpec Exercise(string title, string body, SubmissionMode[]? modes = null, string? was = null) =>
        new(
            title,
            body,
            ContentType.Assignment,
            null,
            GradeScale.None,
            modes ?? _none,
            SubmissionPattern.None,
            _noText,
            _noText,
            _noText,
            was);

    private static PartSpec Assignment(
        string title,
        string body,
        DateTimeOffset due,
        GradeScale scale,
        SubmissionMode[] modes,
        SubmissionPattern pattern,
        string[]? answers = null,
        string[]? feedback = null,
        string[]? needsWork = null,
        string? was = null) => new(
            title,
            body,
            ContentType.Assignment,
            due,
            scale,
            modes,
            pattern,
            answers ?? _genericAnswers,
            feedback ?? _noText,
            needsWork ?? _noText,
            was);

    public static string[] PassFeedback => _passFeedback;

    public static string[] DistinctionFeedback => _distinctionFeedback;

    public static string[] NeedsWorkFeedback => _needsWorkFeedback;

    public static string[] NotSubmittedFeedback => _notSubmittedFeedback;

    /// <summary>Fredag i vecka veckor efter kursstarten, klockan 23.59.</summary>
    private static DateTimeOffset Friday(DateOnly start, int weeksAfter)
    {
        var monday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
        return Se(monday.AddDays((weeksAfter * 7) + 4), 23, 59);
    }
}
