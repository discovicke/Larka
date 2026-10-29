using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.DemoPeople;

namespace Backend.Infrastructure.Persistence;

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
    string[] NeedsWork);

internal sealed record ModuleSpec(
    string Title,
    string Description,
    DateTimeOffset? PublishAt,
    int Completion,
    PartSpec[] Parts,
    bool Sequential = false);

internal sealed record CommentSpec(string Author, string Body, int HoursAfter);

internal sealed record NewsSpec(
    string Title,
    string Body,
    DateTimeOffset At,
    string Author,
    bool Pinned,
    bool InCourse,
    CommentSpec[] Comments);

internal sealed record CourseSpec(
    string Code,
    string Title,
    string Description,
    int Points,
    DateOnly Start,
    DateOnly End,
    string Author,
    ModuleSpec[] Modules,
    NewsSpec[] News);

internal sealed record ClassSpec(
    string Name,
    string Term,
    GroupType Type,
    string[] Teachers,
    string? Mentor,
    int FirstStudent,
    int StudentCount,
    bool HasPublicStudent,
    CourseSpec[] Courses,
    NewsSpec[] News);

/// <summary>
/// Själva demoinnehållet: klasser, kurser, veckor, lektioner, uppgifter och nyheter. Datumen är fasta
/// (hösten 2026, demon är måndagen 2026-11-02), så att något är försenat, något ska in i veckan och något ligger längre fram.
/// </summary>
internal static class DemoCatalog
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

    private static ClassSpec Net25() => new(
        ".NET25",
        "HT25",
        GroupType.Class,
        ["larare", "karin", "anders", "jenny"],
        "malin",
        0,
        21,
        true,
        [
            CSharp("NET25", new DateOnly(2025, 8, 25), new DateOnly(2025, 10, 31), "karin"),
            Databases("NET25", new DateOnly(2025, 11, 3), new DateOnly(2025, 12, 19), "anders"),
            WebApps("NET25", new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 24), "larare"),
            DevOps("NET25", new DateOnly(2026, 10, 12), true, "larare"),
            AiCourse("NET25", new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 18), "jenny", false),
            Lia1()
        ],
        [
            new(
                "Välkommen till Systemutvecklare .NET med AI-kompetens",
                """
                Hej och välkomna till utbildningen **Systemutvecklare .NET med AI-kompetens** (klass .NET25).

                Här i Lärka hittar ni kurserna, veckornas material, uppgifter och betyg. Schemat ligger i kalendern
                och nya nyheter dyker upp i er inkorg. Hör av er till mig om något är oklart.

                /Malin, utbildningsledare
                """,
                Se(2025, 8, 20, 9),
                "malin",
                false,
                false,
                [
                    new("#4", "Tack, ser fram emot starten!", 3),
                    new("#9", "Finns det en lista på vad vi ska ha installerat första dagen?", 5),
                    new("malin", "Ja, den kommer i ett eget inlägg i kursen Programmering med C#.", 7)
                ]),
            new(
                "Schemaändring: föreläsningen i DevOps flyttas till torsdag",
                """
                På grund av sjukdom flyttas föreläsningen om Docker från tisdag 27 oktober till **torsdag 29 oktober,
                kl. 9.00–12.00**. Allt annat i schemat är oförändrat. Lektionssalen är densamma.
                """,
                Se(2026, 10, 23, 15, 10),
                "malin",
                false,
                false,
                [
                    new("#2", "Går det bra att se den i efterhand om man har tandläkartid?", 2),
                    new("malin", "Vi spelar in den och lägger länken i veckans material.", 3)
                ]),
            new(
                "LIA-ansökan öppnar 9 november",
                """
                Nu är det dags att börja tänka på **LIA 1** som startar 18 januari. Från och med 9 november kan ni
                skicka in vilken plats ni valt. Börja redan nu med att ta kontakt med företag, och ta hjälp av
                mig om ni vill ha tips på platser. Instruktionerna finns i kursen LIA 1.
                """,
                Se(2026, 10, 27, 11, 30),
                "malin",
                true,
                false,
                [new("#11", "Får man göra LIA på en annan ort än där man bor nu?", 20)])
        ]);

    private static ClassSpec Net24() => new(
        ".NET24",
        "HT24",
        GroupType.Class,
        ["karin", "patrik", "jenny"],
        "henrik",
        22,
        19,
        false,
        [
            CSharp("NET24", new DateOnly(2024, 8, 26), new DateOnly(2024, 10, 25), "karin"),
            Databases("NET24", new DateOnly(2024, 11, 4), new DateOnly(2024, 12, 20), "karin"),
            WebApps("NET24", new DateOnly(2025, 3, 3), new DateOnly(2025, 4, 25), "jenny"),
            DevOps("NET24", new DateOnly(2026, 3, 2), false, "patrik"),
            AiCourse("NET24", new DateOnly(2026, 4, 13), new DateOnly(2026, 5, 15), "jenny", true),
            Lia2()
        ],
        [
            new(
                "Påminnelse: veckologgen lämnas in varje fredag",
                """
                En kort påminnelse: **veckologgen ska in senast fredag kl. 23.59**. Tre till fem meningar räcker:
                vad du gjort, vad du lärt dig och vad du vill ha hjälp med. Handledaren kan se den om du vill.
                """,
                Se(2026, 10, 26, 8, 45),
                "henrik",
                false,
                false,
                [new("#3", "Ska vi skriva loggen på svenska eller engelska?", 4), new(
                    "henrik",
                    "Det väljer du själv.",
                    6)]),
            new(
                "Studiebesök hos LIA-företagen vecka 46",
                """
                Under vecka 46 besöker jag er på LIA-platserna. Jag bokar tid direkt med er och med era handledare.
                Förbered gärna två frågor och tre saker som du är nöjd med hittills.
                """,
                Se(2026, 10, 28, 13, 0),
                "henrik",
                true,
                false,
                [])
        ]);

    private static ClassSpec Fiber25() => new(
        "FIB25",
        "HT25",
        GroupType.Class,
        ["mikael", "camilla", "roger"],
        "sofie",
        41,
        18,
        false,
        [FiberSplicing(), FiberPon(), FiberLia()],
        [
            new(
                "Välkommen till Fibertekniker",
                """
                Välkommen till utbildningen **Fibertekniker** (klass FIB25). Utbildningen består av teori i
                klassrummet och mycket praktik i vår fiberverkstad. Ta med skyddsglasögon och arbetskläder till
                alla praktiska pass.
                """,
                Se(2025, 8, 21, 10),
                "sofie",
                false,
                false,
                [new("#0", "Behöver man ha egna verktyg eller finns de i verkstaden?", 6), new(
                    "sofie",
                    "Allt finns i verkstaden.",
                    8)]),
            new(
                "Fältdagen flyttad till torsdag 5 november",
                """
                Fältdagen med installationsprovet flyttas från onsdag till **torsdag 5 november**. Samling klockan
                7.30 vid verkstaden. Vi åker tillsammans till övningsanläggningen i Kungsängen.
                """,
                Se(2026, 10, 28, 14, 20),
                "mikael",
                true,
                false,
                [new("#5", "Ska man ha med egen bil eller åker vi gemensamt?", 3), new(
                    "mikael",
                    "Gemensam minibuss från verkstaden.",
                    4)])
        ]);

    private static ClassSpec Committee() => new(
        "Klassråd .NET25",
        "HT26",
        GroupType.Committee,
        ["larare"],
        "malin",
        3,
        3,
        true,
        [],
        [
            new(
                "Kallelse till klassråd torsdag 5 november",
                """
                Hej! Nästa klassråd är **torsdag 5 november kl. 13.00**. På agendan: utvärdering av DevOps-kursen,
                önskemål inför AI-kursen och frågor om LIA. Lägg gärna till punkter i kommentarerna.
                """,
                Se(2026, 10, 26, 10, 15),
                "malin",
                true,
                false,
                [
                    new("#1", "Vi vill gärna ta upp att datorsalen är för varm på eftermiddagarna.", 3),
                    new("elev", "Jag lägger till en punkt om Azure-krediter inför AI-kursen.", 5),
                    new("malin", "Noterat, båda punkterna kommer med.", 8)
                ])
        ]);

    // --- Avslutade kurser som flera klasser läst: samma innehåll, olika datum ---

    private static CourseSpec CSharp(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_CSHARP",
        "Programmering med C#",
        "Grunderna i programmering med C# och .NET: typer, kontrollflöden, objektorientering, samlingar och "
        + "enhetstester.\n\n**Efter kursen kan du**\n\n- skriva och felsöka konsolprogram i C#\n"
        + "- modellera ett problem med klasser och interface\n- skriva enhetstester med xUnit",
        30,
        start,
        end,
        author,
        [
            Week(
                "Grunderna i C#",
                "Typer, kontrollflöden och metoder.",
                start,
                [
                    Lesson(
                        "Typer, variabler och kontrollflöden",
                        "Den här lektionen går igenom `int`, `string`, `bool` och `decimal`, och hur man styr ett "
                        + "program med `if`, `switch` och loopar.\n\n```csharp\nvar summa = 0;\n"
                        + "foreach (var tal in new[] { 1, 2, 3 })\n{\n    summa += tal;\n}\n```"),
                    Lesson(
                        "Metoder och felhantering",
                        "En metod gör en sak. Vi tittar på parametrar, returvärden och hur man hanterar fel med "
                        + "`try`/`catch` utan att gömma dem."),
                    Assignment(
                        "Konsolspel: Tre i rad",
                        "Skriv ett konsolspel där två spelare turas om att placera markörer på ett 3x3-bräde. "
                        + "Programmet ska känna igen vinst och oavgjort. Redovisa hur du delat upp koden i metoder.",
                        Friday(start, 2),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "Objektorienterad programmering",
                "Klasser, arv, interface och LINQ.",
                start.AddDays(21),
                [
                    Lesson(
                        "Klasser, arv och interface",
                        "Vi modellerar verkligheten med klasser och bryter ut gemensamt beteende i interface. "
                        + "Tumregel: föredra komposition framför arv."),
                    Lesson(
                        "Samlingar och LINQ",
                        "`List<T>`, `Dictionary<TKey, TValue>` och LINQ-frågor som `Where`, `Select` och `GroupBy`."),
                    Assignment(
                        "Biblioteksystem med OOP",
                        "Bygg ett litet biblioteksystem med böcker, medlemmar och lån. Använd minst ett interface och "
                        + "visa hur du använder LINQ för att söka. Beskriv dina designval.",
                        Friday(start, 5),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "Testning och kodkvalitet",
                "Enhetstester med xUnit.",
                start.AddDays(42),
                [
                    Lesson(
                        "Enhetstester med xUnit",
                        "Ett test följer Arrange, Act, Assert. Vi skriver tester först för det vi är osäkra på och "
                        + "låter dem fånga fel när koden ändras."),
                    Assignment(
                        "Tester för biblioteksystemet",
                        "Skriv minst åtta enhetstester för biblioteksystemet, varav några för felfall. Redovisa "
                        + "testtäckningen.",
                        Friday(start, 8),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ])
        ],
        []);

    private static CourseSpec Databases(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_DB",
        "Databaser",
        "Relationsdatabaser, SQL och dataåtkomst med Entity Framework Core mot PostgreSQL.",
        25,
        start,
        end,
        author,
        [
            Week(
                "Relationsdatabaser och SQL",
                "Normalisering och frågor.",
                start,
                [
                    Lesson(
                        "Normalisering",
                        "Vi går igenom första till tredje normalformen och varför man delar upp data i tabeller med "
                        + "främmande nycklar."),
                    Lesson(
                        "SELECT, JOIN och aggregering",
                        "Hämta, filtrera och slå ihop data: `SELECT`, `WHERE`, `JOIN`, `GROUP BY` och `HAVING`."),
                    Assignment(
                        "SQL-övningar",
                        "Lös de tolv SQL-uppgifterna mot övningsdatabasen och redovisa dina frågor.",
                        Friday(start, 1),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "EF Core och PostgreSQL",
                "Code first, migrationer och prestanda.",
                start.AddDays(21),
                [
                    Lesson(
                        "Code first och migrationer",
                        "Modellen beskrivs i C#, och migrationer håller databasen i takt med den. Vi tittar på "
                        + "`dotnet ef migrations add` och hur man rullar framåt och bakåt."),
                    Lesson(
                        "Index och prestanda",
                        "När hjälper ett index och när skadar det? Vi läser en query plan med `EXPLAIN`."),
                    Assignment(
                        "Dataåtkomst med EF Core",
                        "Bygg ett repository ovanpå EF Core för ett eget domänproblem. Visa en migration och minst "
                        + "en fråga där du valt att lägga ett index.",
                        Friday(start, 5),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ])
        ],
        []);

    private static CourseSpec WebApps(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_WEBB",
        "Webbapplikationer i C#",
        "Webb-API:er med ASP.NET Core och en klient i Vue, med autentisering och ett teamprojekt.",
        40,
        start,
        end,
        author,
        [
            Week(
                "ASP.NET Core Web API",
                "Routing, validering och JWT.",
                start,
                [
                    Lesson(
                        "Minimal API:er och routing",
                        "Med minimal API:er mappar man `MapGet` och `MapPost` direkt i `Program.cs`. Vi bryter ut "
                        + "logik i tjänster och håller endpoints tunna."),
                    Lesson(
                        "Autentisering med JWT",
                        "En inloggning ger ett token som klienten skickar i `Authorization`-headern. Vi går igenom "
                        + "vad som ska ligga i tokenet och hur lång livslängd det bör ha."),
                    Assignment(
                        "REST-API för bokningar",
                        "Bygg ett API för bokning av rum med validering, felhantering och JWT-skydd. Beskriv "
                        + "endpoints och statuskoder.",
                        Friday(start, 3),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "Frontend med Vue",
                "Komponenter, reaktivitet och API-anrop.",
                start.AddDays(28),
                [
                    Lesson(
                        "Komponenter och reaktivitet",
                        "Vue bygger gränssnittet av komponenter. `ref` och `computed` håller reda på tillstånd."),
                    Lesson(
                        "Anropa API:et",
                        "Hämta data med `fetch`, visa laddning och fel, och håll tokenet på ett ställe."),
                    Assignment(
                        "Klient mot ert API",
                        "Bygg en Vue-klient som listar och skapar bokningar mot ert API. Redovisa hur ni hanterar "
                        + "inloggning och fel.",
                        Friday(start, 6),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "Teamprojekt",
                "Webbapplikation i grupp.",
                start.AddDays(42),
                [
                    Assignment(
                        "Webbapplikation i grupp",
                        "Gör en komplett webbapplikation i grupp, med API, klient och dokumentation. "
                        + "Redovisa vem som gjort vad och hur ni arbetat tillsammans.",
                        Friday(start, 7),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished)
                ])
        ],
        []);

    private static CourseSpec AiCourse(string prefix, DateOnly start, DateOnly end, string author, bool finished)
    {
        var pattern = finished
            ? SubmissionPattern.Finished
            : SubmissionPattern.None;
        return new(
            $".{prefix}_AI",
            "AI-komponenter och ML i Azure",
            "Bygg system med AI-komponenter: språkmodeller, sökning med egen data och maskininlärning i Azure.\n\n"
            + "**Efter kursen kan du**\n\n- anropa språkmodeller från .NET\n- bygga en assistent som svarar utifrån "
            + "egna dokument (RAG)\n- träna och driftsätta en enkel modell i Azure ML\n- resonera om ansvarsfull AI",
            30,
            start,
            end,
            author,
            [
                Week(
                    "Azure AI-tjänster",
                    "Språkmodeller och egen data.",
                    start,
                    [
                        Lesson(
                            "Översikt: Azure AI Foundry",
                            "Vilka tjänster finns och när väljer man vilken? Vi tittar på modellkatalogen, kostnader "
                            + "och kvoter."),
                        Lesson(
                            "Anropa språkmodeller från .NET",
                            "Med klientbiblioteket skickar vi en prompt och strömmar tillbaka svaret. Vi pratar om "
                            + "systemprompt, temperatur och hur man håller nere kostnaden."),
                        Assignment(
                            "Chattassistent med egen data",
                            "Bygg en assistent som svarar på frågor utifrån dokument ni själva väljer (RAG). Redovisa "
                            + "hur ni delar upp och söker i dokumenten.",
                            Friday(start, 1),
                            GradeScale.PassFailDistinction,
                            _text,
                            pattern)
                    ]),
                Week(
                    "Maskininlärning i Azure",
                    "Träna, driftsätta och bedöma.",
                    start.AddDays(14),
                    [
                        Lesson(
                            "Träna och driftsätta en modell",
                            "Från data till en endpoint: förbereda data, träna i Azure ML och driftsätta modellen."),
                        Lesson(
                            "Ansvarsfull AI",
                            "Bias, transparens och personuppgifter. Vi diskuterar vad man bör dokumentera och testa."),
                        Assignment(
                            "Klassificering med Azure ML",
                            "Träna en klassificeringsmodell på ett öppet dataset och driftsätt den. Redovisa "
                            + "utvärderingen och modellens begränsningar.",
                            Friday(start, 4),
                            GradeScale.PassFailDistinction,
                            _text,
                            pattern)
                    ])
            ],
            finished
                ? []
                : [
                    new(
                        "Kursstart 16 november: förbered ett Azure-konto",
                        """
                        Inför kursstarten behöver du ett Azure-konto med skolans utbildningskrediter. Följ guiden i
                        första veckan i kursen och hör av dig om det inte fungerar innan vi startar.
                        """,
                        Se(2026, 10, 28, 12, 40),
                        author,
                        false,
                        true,
                        [new("#7", "Räcker det med kontot vi fick i Databaser-kursen?", 6)])
                ]);
    }

    private static CourseSpec Lia1()
    {
        var start = new DateOnly(2027, 1, 18);
        return new(
            ".NET25_LIA1",
            "LIA 1",
            "Första perioden lärande i arbete: 18 veckor på ett företag där du arbetar som utvecklare under en "
            + "handledare.",
            75,
            start,
            new DateOnly(2027, 5, 28),
            "malin",
            [
                new(
                    "Inför LIA",
                    "Hitta plats, ansök och förbered dig.",
                    null,
                    0,
                    [
                        Lesson(
                            "Så hittar du en LIA-plats",
                            "Börja med nätverket, branschevenemang och företag som annonserat. Skriv ner tre företag "
                            + "du vill till och kontakta dem personligen."),
                        Lesson(
                            "Personligt brev och CV",
                            "Håll brevet kort och konkret: vad du kan, vad du vill lära dig och varför just det "
                            + "företaget. Länka till ett projekt du är stolt över."),
                        Assignment(
                            "Skicka in vald LIA-plats",
                            "Berätta vilket företag du valt, vem som är handledare och vad du ska arbeta med. "
                            + "Utbildningsledaren godkänner platsen.",
                            Se(2026, 11, 20, 23, 59),
                            GradeScale.PassFail,
                            _text,
                            SubmissionPattern.None)
                    ]),
                new(
                    "Under LIA",
                    "Veckologg och uppföljning.",
                    Se(start, 7),
                    0,
                    [
                        Lesson(
                            "Din roll under LIA",
                            "Du är en del av teamet. Ställ frågor, be om feedback och skriv ner vad du lär dig varje "
                            + "vecka.")
                    ])
            ],
            []);
    }

    private static CourseSpec Lia2()
    {
        var start = new DateOnly(2026, 8, 24);
        var weeklyLog = new[]
        {
            "Den här veckan har jag arbetat med ett nytt API-endpoint och skrivit tester för det. Jag lärde mig hur "
            + "teamet granskar pull requests. Jag vill ha hjälp med hur vi felsöker i produktionsmiljön.",
            "Jag har suttit med i en produktionsincident och fått följa felsökningen. Det var lärorikt att se hur "
            + "teamet kommunicerade. Nästa vecka tar jag över en liten bugg själv.",
            "Veckan gick åt till att bygga en vy i frontend och rätta feedback från granskningen. Jag lärde mig mer "
            + "om tillgänglighet. Jag vill gärna prata om hur man planerar sin tid."
        };

        PartSpec WeekLog(int week, DateOnly friday, SubmissionPattern pattern) => Assignment(
            $"Veckologg vecka {week}",
            "Skriv tre till fem meningar: vad du gjort, vad du lärt dig och vad du vill ha hjälp med.",
            Se(friday, 23, 59),
            GradeScale.PassFail,
            _text,
            pattern,
            weeklyLog);

        return new(
            ".NET24_LIA2",
            "LIA 2",
            "Andra perioden lärande i arbete. Du arbetar på ditt LIA-företag och följer upp med veckologg, "
            + "halvtidsutvärdering och en rapport.",
            100,
            start,
            new DateOnly(2026, 11, 27),
            "henrik",
            [
                new(
                    "Start och mål",
                    "Individuell plan för LIA.",
                    Se(start, 7),
                    92,
                    [
                        Lesson(
                            "Dina lärandemål under LIA",
                            "Formulera tre mål ni följer upp med handledaren. Ett bra mål är konkret och går att "
                            + "kontrollera."),
                        Lesson(
                            "Handledarsamtal: checklista",
                            "Boka återkommande samtal med handledaren. Förbered frågor och skriv ner det ni kommer "
                            + "överens om."),
                        Assignment(
                            "Individuell plan för LIA",
                            "Skriv din plan för perioden med tre lärandemål och hur du ska nå dem.",
                            Se(2026, 9, 4, 23, 59),
                            GradeScale.PassFail,
                            _text,
                            SubmissionPattern.Finished)
                    ]),
                new(
                    "Löpande veckologg",
                    "En kort logg varje fredag.",
                    Se(2026, 9, 28, 7),
                    80,
                    [
                        Lesson(
                            "Så skriver du en bra veckologg",
                            "En bra logg är kort och konkret. Skriv vad du faktiskt gjort, inte bara vad du har "
                            + "varit med om."),
                        WeekLog(40, new DateOnly(2026, 10, 2), SubmissionPattern.Finished),
                        WeekLog(41, new DateOnly(2026, 10, 9), SubmissionPattern.Finished),
                        WeekLog(42, new DateOnly(2026, 10, 16), SubmissionPattern.Finished),
                        WeekLog(43, new DateOnly(2026, 10, 23), SubmissionPattern.MostlyGraded),
                        WeekLog(44, new DateOnly(2026, 10, 30), SubmissionPattern.Early)
                    ]),
                new(
                    "Utvärdering och avslut",
                    "Halvtid, rapport och handledarens bedömning.",
                    Se(2026, 10, 12, 7),
                    40,
                    [
                        Assignment(
                            "Halvtidsutvärdering",
                            "Utvärderingen sker på plats tillsammans med handledaren och utbildningsledaren.",
                            Se(2026, 10, 16, 16, 0),
                            GradeScale.PassFail,
                            _onSite,
                            SubmissionPattern.Finished),
                        Assignment(
                            "LIA-rapport",
                            "Skriv en rapport på två till tre sidor om vad du arbetat med, vad du lärt dig och hur "
                            + "det kopplar till kursmålen.",
                            Se(2026, 11, 20, 23, 59),
                            GradeScale.PassFailDistinction,
                            _text,
                            SubmissionPattern.None),
                        Assignment(
                            "Handledarens slutbedömning",
                            "Handledaren bedömer ditt arbete på plats vid periodens slut.",
                            Se(2026, 11, 27, 15, 0),
                            GradeScale.PassFail,
                            _onSite,
                            SubmissionPattern.None)
                    ])
            ],
            [
                new(
                    "Så fungerar LIA 2",
                    """
                    Under LIA 2 arbetar du på ditt företag och följer upp med mig varje vecka via veckologgen.
                    Halvtidsutvärderingen är genomförd, och nästa steg är LIA-rapporten som lämnas in 20 november.
                    """,
                    Se(2026, 8, 21, 9, 30),
                    "henrik",
                    false,
                    true,
                    [])
            ]);
    }

    // --- Fibertekniker: praktiskt arbete, bedömning på plats ---

    private static CourseSpec FiberSplicing() => new(
        "FIB25_SKARV",
        "Skarvning och mätning",
        "Säkert arbete med fiber, smältskarvning och mätning med OTDR. Mycket av kursen är praktisk och bedöms "
        + "på plats i verkstaden.",
        40,
        new DateOnly(2025, 10, 6),
        new DateOnly(2026, 1, 30),
        "mikael",
        [
            Week(
                "Säkerhet vid fiberarbete",
                "Laser, kemikalier och arbetsmiljö.",
                new DateOnly(2025, 10, 6),
                [
                    Lesson(
                        "Laserklasser och ögonskydd",
                        "Fiberoptisk laser är osynlig men farlig. Titta aldrig in i en ansluten fiber, och använd "
                        + "alltid skyddsglasögon vid arbete med aktiv utrustning."),
                    Lesson(
                        "Skärande avfall och kemikalier",
                        "Fibersmulor är vassa och ska samlas i en särskild behållare. Sprit och rengöringsmedel "
                        + "förvaras och hanteras enligt datablad."),
                    Assignment(
                        "Egenkontroll: riskbedömning",
                        "Gör en riskbedömning för ett tänkt arbetsplatsbesök. Beskriv tre risker, hur du "
                        + "förebygger dem och vem du ska kontakta vid en olycka.",
                        Friday(new DateOnly(2025, 10, 6), 1),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Risker: ögonskada av laser, skärskador av fibersmulor, fall från stege. Åtgärder: "
                            + "skyddsglasögon, avfallsburk vid arbetsplatsen, stege med kontrollerad fotplatta. "
                            + "Vid olycka ringer jag 112 och sedan arbetsledaren."
                        ])
                ]),
            Week(
                "Smältskarvning",
                "Förbereda, klyva och skarva.",
                new DateOnly(2025, 11, 3),
                [
                    Lesson(
                        "Rengöring och klyvning",
                        "Bra skarvar börjar med ren och rakt klyvd fiber. Vi går igenom avisolering, rengöring med "
                        + "sprit och hur klyvverktyget ställs in."),
                    Lesson(
                        "Smältskarvning steg för steg",
                        "Placera fibern i skarvaren, kontrollera vinkeln, kör skarvprogrammet och läs dämpningen. "
                        + "Under 0,1 dB är målet."),
                    Assignment(
                        "Skarvprov på plats",
                        "Du skarvar tolv fibrar i verkstaden inför läraren. Alla skarvar ska vara under 0,1 dB.",
                        Friday(new DateOnly(2025, 11, 3), 3),
                        GradeScale.PassFailDistinction,
                        _onSite,
                        SubmissionPattern.Finished)
                ]),
            Week(
                "Mätning med OTDR",
                "Mäta och tolka kurvor.",
                new DateOnly(2025, 12, 1),
                [
                    Lesson(
                        "OTDR-mätning och tolkning",
                        "Kurvan visar hela länken. Vi lär oss hitta skarvar, kontakter, böjar och brott, och skriva "
                        + "ett mätprotokoll."),
                    Assignment(
                        "Mätprotokoll från fältövning",
                        "Mät en länk på tre kilometer och fyll i mätprotokollet. Ange dämpning per skarv och total "
                        + "dämpning, och kommentera avvikelser.",
                        Friday(new DateOnly(2025, 12, 1), 2),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Länk 3,02 km, våglängd 1550 nm. Total dämpning 1,1 dB. Skarvar: 0,03, 0,05, 0,04 dB. "
                            + "Ingen avvikelse, men kontakten vid änden hade 0,6 dB, så den rengjordes och mättes om "
                            + "till 0,3 dB."
                        ])
                ])
        ],
        []);

    private static CourseSpec FiberPon() => new(
        "FIB25_PON",
        "Passiva fibernät och installation",
        "Passiva optiska nät (PON): uppbyggnad, installation hos kund och dokumentation. Kursen avslutas med ett "
        + "praktiskt installationsprov.",
        35,
        new DateOnly(2026, 10, 5),
        new DateOnly(2026, 11, 27),
        "roger",
        [
            Week(
                "Uppbyggnad av PON-nät",
                "OLT, splitter och ONT.",
                new DateOnly(2026, 10, 5),
                [
                    Lesson(
                        "Från central till kund: OLT, splitter, ONT",
                        "Ett PON-nät delar en fiber mellan flera kunder med en passiv splitter. Vi går igenom "
                        + "uppbyggnaden och vilka dämpningar som är rimliga."),
                    Lesson(
                        "Dämpningsbudget",
                        "Räkna ihop fiberns, skarvarnas, kontakternas och splitterns dämpning, och jämför med "
                        + "utrustningens gränsvärden."),
                    Assignment(
                        "Dimensionera ett PON-nät",
                        "Rita ett nät för 48 hushåll och räkna ut dämpningsbudgeten för det längsta avståndet. "
                        + "Redovisa antagandena.",
                        Friday(new DateOnly(2026, 10, 5), 2),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.MostlyGraded,
                        [
                            "Nätet har en OLT, en 1:8-splitter i stolpskåpet och 1:8 vid gatan. Längsta avstånd är "
                            + "4,2 km. Dämpningsbudget: fiber 1,5 dB, 6 skarvar 0,3 dB, 4 kontakter 1,6 dB, "
                            + "splitter 21 dB. Summa 24,4 dB, vilket ryms inom klass B+."
                        ])
                ]),
            Week(
                "Installation hos kund",
                "Dragning, uttag och idrifttagning.",
                new DateOnly(2026, 10, 19),
                [
                    Lesson(
                        "Installation steg för steg",
                        "Från markförläggning till uttag i fastigheten. Vi går igenom kabelvägar, minsta böjradie "
                        + "och hur man tar i drift."),
                    Lesson(
                        "Kundbemötande och arbetsmiljö",
                        "Du representerar företaget hos kunden. Förklara vad du gör, skydda golv och möbler, och "
                        + "städa efter dig."),
                    Assignment(
                        "Dokumentation av färdig anläggning",
                        "Dokumentera den installation du gjorde under fältövningen: kabelväg, märkning, "
                        + "mätvärden och foton (beskriv vad bilderna visar).",
                        Se(2026, 10, 30, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Early),
                    Assignment(
                        "Installationsprov hos kund",
                        "Praktiskt prov på övningsanläggningen. Du installerar ett uttag och tar anläggningen i "
                        + "drift inför läraren.",
                        Se(2026, 11, 5, 15, 0),
                        GradeScale.PassFailDistinction,
                        _onSite,
                        SubmissionPattern.None)
                ]),
            Week(
                "Dokumentation och slutmätning",
                "Mätprotokoll och överlämning.",
                new DateOnly(2026, 11, 2),
                [
                    Lesson(
                        "Mätprotokoll och överlämning",
                        "En anläggning är inte klar förrän den är dokumenterad. Vi går igenom vad ett mätprotokoll "
                        + "ska innehålla och hur anläggningen lämnas över."),
                    Assignment(
                        "Slutmätning och mätprotokoll",
                        "Mät den installerade anläggningen med OTDR och effektmätare, och lämna in protokollet.",
                        Se(2026, 11, 13, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.None)
                ])
        ],
        [
            new(
                "Praktiskt prov torsdag 5 november",
                """
                Installationsprovet genomförs på övningsanläggningen. Ta med skyddsglasögon, arbetskläder och
                ID. Du får tolv minuter per moment, och läraren bedömer på plats.
                """,
                Se(2026, 10, 27, 15, 5),
                "roger",
                false,
                true,
                [])
        ]);

    private static CourseSpec FiberLia() => new(
        "FIB25_LIA1",
        "Fältpraktik (LIA) 1",
        "Första perioden lärande i arbete som fibertekniker hos ett installationsföretag.",
        60,
        new DateOnly(2027, 1, 18),
        new DateOnly(2027, 4, 30),
        "sofie",
        [
            new(
                "Inför praktiken",
                "Hitta plats och förbered dig.",
                null,
                0,
                [
                    Lesson(
                        "Så hittar du en praktikplats",
                        "Kontakta installatörer och nätägare i regionen. Ta med intyg om utförd säkerhetsutbildning "
                        + "när du söker."),
                    Assignment(
                        "Anmäl din praktikplats",
                        "Ange företag, handledare och startdatum.",
                        Se(2026, 11, 27, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.None)
                ])
        ],
        []);

    // --- DevOps: den pågående kursen (och samma kurs, avslutad, för äldre klasser) ---

    private static CourseSpec DevOps(string prefix, DateOnly firstMonday, bool current, string author)
    {
        var end = firstMonday.AddDays(32);
        PartSpec DevOpsTask(int dayOffset, GradeScale scale, string title, string body, SubmissionPattern pattern,
            string[] answers, string[]? feedback = null, string[]? needsWork = null) => Assignment(
            title,
            body,
            Se(firstMonday.AddDays(dayOffset), 23, 59),
            scale,
            _text,
            current
                ? pattern
                : SubmissionPattern.Finished,
            answers,
            feedback,
            needsWork);

        var gradedA2 = new[]
        {
            "Pipelinen bygger och testar, bra. Lägg till cache för NuGet så går bygget snabbare.",
            "Tydligt upplagt och väl redovisat. Godkänt.",
            "Bra: ni har även en matris för flera .NET-versioner och hanterar secrets på rätt sätt."
        };
        var needsWorkA2 = new[]
        {
            "Jag saknar en redovisning av hur ni hanterar secrets. Komplettera det och lämna in igen.",
            "Nästan godkänt: workflowet saknar tester för frontend. Lägg till det och lämna in igen.",
            "Pipelinen bygger men kör inte testerna. Lägg till teststeget och visa en körning som fångar ett fel."
        };

        // Veckornas publicering: v45 och v46 öppnas på fredagen veckan innan, som läraren brukar göra.
        DateTimeOffset? WeekOpen(int week) => current && week >= 3
            ? Se(firstMonday.AddDays((week * 7) - 3), 12)
            : Se(firstMonday.AddDays(week * 7), 7);

        return new(
            $".{prefix}_DEVOPS",
            "DevOps",
            "Versionshantering, kontinuerlig integration, containrar, automatiserade tester och driftsättning i "
            + "molnet. Vi arbetar i team och släpper kod hela vägen från commit till produktion.\n\n"
            + "**Efter kursen kan du**\n\n- arbeta i ett Git-flöde med pull requests och kodgranskning\n"
            + "- bygga och testa automatiskt med GitHub Actions\n- paketera en applikation i en container\n"
            + "- versionera och släppa en tjänst och driftsätta den i molnet",
            25,
            firstMonday,
            end,
            author,
            [
                new(
                    "Vecka 42 – Git-flöden och samarbete",
                    "Hur ett team delar upp arbetet i branchar och granskar varandras kod.",
                    WeekOpen(0),
                    92,
                    [
                        Lesson(
                            "Git-flöden: GitHub Flow, GitFlow och trunk-based",
                            """
                            Ett Git-flöde är teamets överenskommelse om hur kod når `main`.

                            - **GitHub Flow**: korta feature-branchar, pull request, merge till `main`, driftsätt.
                            - **GitFlow**: långlivade `develop`- och `release`-branchar. Passar när man släpper sällan.
                            - **Trunk-based**: alla jobbar mot `main` med mycket små ändringar och feature flags.

                            Välj det enklaste som fungerar för ert team. De flesta behöver inte GitFlow.
                            """),
                        Lesson(
                            "Pull requests och kodgranskning",
                            """
                            En bra pull request är **liten**, har en tydlig beskrivning och går att granska på
                            tio minuter.

                            1. Beskriv *varför* ändringen görs, inte bara vad.
                            2. Länka ärendet (`Closes #123`).
                            3. Be om granskning av någon som kan området.

                            Som granskare: fråga hellre än att beordra, och godkänn först när du förstått koden.
                            """),
                        Lesson(
                            "Branch protection och CODEOWNERS",
                            """
                            Skydda `main` så att ingen kan pusha direkt:

                            - kräv pull request och minst en godkänd granskning
                            - kräv att CI är grön innan merge
                            - använd `CODEOWNERS` så att rätt person granskar rätt del av koden

                            ```text
                            # .github/CODEOWNERS
                            /backend/  @org/backend-team
                            /client/   @org/frontend-team
                            ```
                            """),
                        Reading(
                            "Läs: The Phoenix Project, kapitel 1–4",
                            "Läs kapitel 1–4 och anteckna tre saker du känner igen från ert eget projekt. Vi pratar "
                            + "om dem på torsdagens lektion. Klarmarkera när du läst.",
                            Se(firstMonday.AddDays(4), 23, 59)),
                        DevOpsTask(
                            11,
                            GradeScale.PassFail,
                            "Git-flöde i teamet",
                            "Välj ett Git-flöde för ert projekt och motivera valet i några stycken. Beskriv hur ni "
                            + "namnger branchar, hur ni granskar och vad som krävs för att merga.",
                            SubmissionPattern.MostlyGraded,
                            [
                                "Vi valde GitHub Flow: `main` är alltid driftsättningsbar och allt arbete sker i "
                                + "korta feature-branchar med namn som `feature/123-login`. Varje pull request "
                                + "granskas av minst en kollega, och branch protection kräver grön CI. GitFlow "
                                + "kändes för tungt för ett team på fem personer som släpper ofta.",
                                "Vi kör trunk-based med mycket små pull requests. Vi har feature flags för det som inte "
                                + "är klart och slår ihop till `main` flera gånger om dagen. Granskning sker "
                                + "inom en timme enligt vår teamöverenskommelse.",
                                "GitHub Flow, med skillnaden att vi har en `release`-tagg per vecka. Branchar heter "
                                + "`typ/ärende-kort-beskrivning` och kräver en granskare. CODEOWNERS pekar ut vem "
                                + "som granskar backend respektive klient."
                            ])
                    ]),
                new(
                    "Vecka 43 – Kontinuerlig integration med GitHub Actions",
                    "Bygg och testa automatiskt vid varje ändring.",
                    WeekOpen(1),
                    88,
                    [
                        Lesson(
                            "Så fungerar GitHub Actions",
                            """
                            Ett **workflow** ligger i `.github/workflows/` och består av **jobb** som körs på
                            **runners**. Det startas av **händelser** som `push` och `pull_request`.

                            ```yaml
                            name: ci
                            on: [push, pull_request]
                            jobs:
                              build:
                                runs-on: ubuntu-latest
                                steps:
                                  - uses: actions/checkout@v4
                                  - uses: actions/setup-dotnet@v4
                                    with:
                                      dotnet-version: 10.0.x
                                  - run: dotnet build --no-restore -warnaserror
                                  - run: dotnet test
                            ```
                            """),
                        Lesson(
                            "Matrisbyggen, cache och artefakter",
                            """
                            Med en **matris** kör du samma jobb på flera versioner eller operativsystem.
                            Cachea beroenden (`actions/cache`) så att bygget går snabbare, och spara resultat som
                            **artefakter** (`actions/upload-artifact`) så att nästa jobb eller en människa kan använda
                            dem.
                            """),
                        Lesson(
                            "Secrets och miljöer",
                            """
                            Lägg aldrig lösenord i koden. Använd **secrets** i repot eller i en **miljö**
                            (`environment: production`) som kan kräva godkännande innan jobbet körs.

                            Hemligheter maskas i loggen, men skriv dem aldrig med `echo` ändå.
                            """),
                        DevOpsTask(
                            16,
                            GradeScale.PassFailDistinction,
                            "CI-pipeline för er backend",
                            "Skapa ett workflow som bygger och testar backend vid varje push och pull request. "
                            + "Redovisa vad pipelinen gör, hur ni cachar beroenden och hur ni hanterar secrets.",
                            SubmissionPattern.Queue,
                            [
                                "Vårt workflow kör `dotnet build -warnaserror` och `dotnet test` på varje push och "
                                + "pull request. Vi cachar NuGet med `actions/cache` och testerna körs mot en "
                                + "Postgres-container via Testcontainers. Secrets ligger i en miljö som kräver "
                                + "godkännande för produktion.",
                                "Pipelinen har tre jobb: bygg, test och lint. Bygget tar ungefär två minuter. Vi "
                                + "laddar upp testresultat som artefakt och bryter bygget om täckningen understiger "
                                + "70 procent. Inga hemligheter finns i koden.",
                                "Vi har ett workflow med matris över .NET 9 och 10 för att se att vi inte bryter "
                                + "något vid uppgradering. Pull requests kan inte mergas förrän båda är gröna."
                            ],
                            gradedA2,
                            needsWorkA2)
                    ], true),
                new(
                    "Vecka 44 – Docker och containrar",
                    "Paketera applikationen så att den körs likadant överallt.",
                    WeekOpen(2),
                    75,
                    [
                        Lesson(
                            "Docker i korthet: images, lager och containrar",
                            """
                            En **image** är en mall, en **container** är en körande instans av den. Images byggs i
                            **lager** som cachas, så ordningen i en `Dockerfile` spelar roll för hur snabbt bygget går.

                            ```bash
                            docker build -t larka:dev .
                            docker run --rm -p 8080:8080 larka:dev
                            ```
                            """),
                        Lesson(
                            "Dockerfile med multi-stage build",
                            """
                            Bygg i en stor image och kör i en liten:

                            ```dockerfile
                            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
                            WORKDIR /src
                            COPY . .
                            RUN dotnet publish backend -c Release -o /app

                            FROM mcr.microsoft.com/dotnet/aspnet:10.0
                            WORKDIR /app
                            COPY --from=build /app .
                            ENTRYPOINT ["dotnet", "backend.dll"]
                            ```

                            Den färdiga imagen innehåller ingen SDK och blir mycket mindre.
                            """),
                        Lesson(
                            "docker compose för utveckling",
                            """
                            Med `docker compose` startar du appen tillsammans med databasen med ett kommando.
                            Använd en volym för databasen, och en `healthcheck` så att appen väntar på att den är
                            redo.
                            """),
                        DevOpsTask(
                            18,
                            GradeScale.PassFailDistinction,
                            "Containerisera applikationen",
                            "Skriv en `Dockerfile` med multi-stage build för er applikation och en "
                            + "`docker-compose.yml` som startar den tillsammans med databasen. Redovisa imagens "
                            + "storlek och vad ni gjorde för att hålla den liten.",
                            SubmissionPattern.Early,
                            [
                                "Vi har en multi-stage `Dockerfile` där bygget sker i SDK-imagen och körningen i "
                                + "`aspnet`. Imagen blev 215 MB. Compose startar appen och Postgres med en volym och "
                                + "en healthcheck.",
                                "Dockerfile med multi-stage och en `.dockerignore` som utesluter `bin`, `obj` och "
                                + "`node_modules`. Imagen gick från 1,2 GB till 230 MB. Compose-filen har en "
                                + "separat profil för tester."
                            ])
                    ], true),
                new(
                    "Vecka 45 – Automatiska tester i pipelinen",
                    "Testpyramiden, integrationstester och end-to-end.",
                    WeekOpen(3),
                    25,
                    [
                        Lesson(
                            "Testpyramiden och vad som ska testas",
                            """
                            Många snabba enhetstester längst ned, färre integrationstester i mitten och ett fåtal
                            end-to-end-tester överst. Ju högre upp, desto långsammare och skörare.

                            Testa beteende, inte implementation. Ett test som går sönder av en omstrukturering utan
                            att något fel finns är ett dåligt test.
                            """),
                        Lesson(
                            "Integrationstester med Testcontainers",
                            """
                            Testa mot en riktig databas i stället för en låtsas. **Testcontainers** startar en
                            Postgres-container åt testerna och släcker den efteråt.

                            ```csharp
                            var postgres = new PostgreSqlBuilder().Build();
                            await postgres.StartAsync();
                            ```
                            """),
                        Lesson(
                            "End-to-end med Playwright i CI",
                            """
                            Playwright styr en riktig webbläsare. Kör testerna i pipelinen mot en tillfällig
                            miljö, och spara trace och skärmdumpar som artefakter när något går fel.
                            """),
                        DevOpsTask(
                            25,
                            GradeScale.PassFailDistinction,
                            "Testautomation i CI",
                            "Lägg till integrationstester och minst ett end-to-end-test, och kör dem i pipelinen. "
                            + "Redovisa vad som testas, hur lång tid det tar och hur ni hanterar instabila tester.",
                            SubmissionPattern.None,
                            [
                                "Backend har 140 enhetstester och 25 integrationstester mot Postgres via "
                                + "Testcontainers. Dessutom ett Playwright-test för inloggningen. Hela sviten tar fyra "
                                + "minuter i pipelinen. Instabila tester sätter vi i karantän och rättar inom en "
                                + "vecka.",
                                "Vi har lagt till integrationstester för alla endpoints och ett end-to-end-test som "
                                + "loggar in och lämnar in en uppgift. Playwright körs i ett eget jobb och sparar "
                                + "trace som artefakt när något går fel."
                            ])
                    ]),
                new(
                    "Vecka 46 – Versionering, release och driftsättning",
                    "Från tagg till produktion, och tillbaka igen om det går fel.",
                    WeekOpen(4),
                    0,
                    [
                        Lesson(
                            "Semantisk versionering och changelog",
                            """
                            **MAJOR.MINOR.PATCH**:

                            - höj `MAJOR` när något går sönder för användarna
                            - höj `MINOR` när du lägger till något bakåtkompatibelt
                            - höj `PATCH` för buggrättningar

                            Med *conventional commits* (`feat:`, `fix:`, `feat!:`) kan versionen och changelogen
                            räknas ut automatiskt.
                            """),
                        Lesson(
                            "Release-flöden: taggar och GitHub Releases",
                            """
                            En release är en tagg på en commit som gick igenom hela pipelinen. Låt pipelinen skapa
                            tagg, release notes och den image som driftsätts, så att det som testats är exakt det som
                            släpps.
                            """),
                        Lesson(
                            "Driftsättning: miljöer, rollback och övervakning",
                            """
                            Driftsätt till en testmiljö först. Ha en plan för **rollback** innan du släpper, och
                            övervaka efteråt: hälsokontroller, loggar och larm. En driftsättning är inte klar förrän
                            du sett att den fungerar.
                            """),
                        DevOpsTask(
                            32,
                            GradeScale.PassFailDistinction,
                            "Releasa och driftsätt",
                            "Släpp en version av er tjänst med semantisk versionering, en automatisk changelog och "
                            + "driftsättning till molnet. Visa hur ni rullar tillbaka och vad ni övervakar.",
                            SubmissionPattern.None,
                            [
                                "Version 1.2.0 släpptes via en tagg som pipelinen bygger till en image och "
                                + "driftsätter till testmiljön och sedan produktion. Changelogen genereras av "
                                + "conventional commits. Vid fel rullar vi tillbaka till föregående tagg, och vi "
                                + "övervakar hälsokontrollen och felloggen i tio minuter efter varje release.",
                                "Vi släppte 2.0.0 eftersom ett API-svar ändrade form. Driftsättningen sker med "
                                + "godkännande i miljön production. Rollback gör vi genom att köra det gamla "
                                + "workflowet med föregående tagg. Vi övervakar felfrekvens och svarstid."
                            ])
                    ])
            ],
            current
                ? DevOpsNews(author)
                : []);
    }

    private static NewsSpec[] DevOpsNews(string author) =>
    [
        new(
            "Välkommen till DevOps",
            """
            Nu startar DevOps-kursen, **12 oktober till 13 november**. Vi arbetar i team med ett gemensamt
            projekt och släpper kod hela vägen till produktion. Varje vecka har material och en uppgift, och
            deadlines hittar ni i kalendern.

            Börja med att läsa veckans material och skapa ett repo för teamet.
            """,
            Se(2026, 10, 9, 14, 0),
            author,
            true,
            true,
            [
                new("#2", "Ska vi vara samma team som i förra kursen?", 4),
                new(author, "Ja, ni fortsätter i samma team om ingen har önskemål om byte.", 6)
            ]),
        new(
            "Påminnelse: CI-uppgiften lämnas in onsdag kväll",
            """
            En påminnelse: **CI-pipelinen ska in senast onsdag 28 oktober kl. 23.59**. Glöm inte att redovisa hur ni
            hanterar secrets, det är det som oftast saknas. Fastnar ni, kom på handledningen på tisdag eftermiddag.
            """,
            Se(2026, 10, 26, 9, 15),
            author,
            false,
            true,
            [
                new("#5", "Kan vi lämna in länken till repot, eller måste vi skriva allt i svaret?", 3),
                new(author, "Länken räcker som komplement, men skriv ändå en kort redovisning.", 5)
            ]),
        new(
            "Gästföreläsning om driftsättning och jour, fredag 6 november",
            """
            På fredag nästa vecka kommer en gäst som arbetar med drift och berättar hur ett riktigt team släpper
            kod flera gånger om dagen, och hur det är att ha jour. Ta gärna med frågor. Föreläsningen spelas in.
            """,
            Se(2026, 10, 28, 16, 30),
            author,
            false,
            true,
            [new("#9", "Vad bra, finns det en chans att få ställa frågor om hur man blir DevOps-ingenjör?", 18)])
    ];

    // --- Byggstenar ---

    private static ModuleSpec Week(string title, string description, DateOnly start, PartSpec[] parts) =>
        new(title, description, Se(start, 7), 92, parts);

    private static PartSpec Lesson(string title, string body) =>
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
            _noText);

    /// <summary>En uppgift utan inlämning. Eleven klarmarkerar den själv.</summary>
    private static PartSpec Reading(string title, string body, DateTimeOffset due) =>
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
            _noText);

    private static PartSpec Assignment(
        string title,
        string body,
        DateTimeOffset due,
        GradeScale scale,
        SubmissionMode[] modes,
        SubmissionPattern pattern,
        string[]? answers = null,
        string[]? feedback = null,
        string[]? needsWork = null) => new(
            title,
            body,
            ContentType.Assignment,
            due,
            scale,
            modes,
            pattern,
            answers ?? _genericAnswers,
            feedback ?? _noText,
            needsWork ?? _noText);

    public static string[] PassFeedback => _passFeedback;

    public static string[] DistinctionFeedback => _distinctionFeedback;

    public static string[] NeedsWorkFeedback => _needsWorkFeedback;

    /// <summary>Fredag i vecka veckor efter kursstarten, klockan 23.59.</summary>
    private static DateTimeOffset Friday(DateOnly start, int weeksAfter)
    {
        var monday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
        return Se(monday.AddDays((weeksAfter * 7) + 4), 23, 59);
    }
}
