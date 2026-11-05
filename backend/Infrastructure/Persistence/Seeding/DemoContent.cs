using Backend.Domain.Enums;

namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Texterna i demodatan: tre klasser med en avslutad, en pågående och en kommande kurs. Dagarna räknas från kursens
/// första dag. DemoSeed lägger kurserna så att den pågående kursen har en vecka öppen just nu, med en uppgift
/// vars deadline ligger mer än en vecka fram, så att demon går att visa några dagar efter att datan skapats.
/// </summary>
internal static class DemoContent
{
    public static readonly IReadOnlyDictionary<string, DemoClass> Classes = new Dictionary<string, DemoClass>
    {
        [".NET25"] = new(
            WebApp,
            DevOps,
            Lia,
            CurrentDaysLeft: 18,
            FeaturedStudent: "elev",
            HelpMessage: "Jag får inte containern att starta i molnet. Loggen säger att porten redan används."),
        ["JAVA25"] = new(Java, SpringBoot, Testing, CurrentDaysLeft: 35),
        ["FIBER25"] = new(
            FiberTech,
            Network,
            FieldProject,
            CurrentDaysLeft: 50,
            FeaturedStudent: "elev-fiber",
            HelpMessage: "Ska alla OTDR-kurvor med i överlämningsdokumentet, eller räcker en tabell med mätvärdena?"),
    };

    // --- .NET25 ---------------------------------------------------------------------------------------------------

    private static DemoCourse WebApp => new(
        "WeApp",
        "Webbapplikationer i C#, ASP.NET",
        "Eleverna bygger en webbapplikation med ASP.NET Core och Entity Framework Core, från första endpoint till "
        + "ett färdigt slutprojekt.",
        40,
        70,
        [
            new DemoWeek(
                "Vecka 1: ASP.NET Core från grunden",
                "Hur en request går genom appen, och ditt första API.",
                0,
                [
                    new DemoTheory(
                        "Request, middleware och routing",
                        """
                        En request till en ASP.NET Core-app går genom en kedja av **middleware** innan den når din
                        endpoint. Ordningen i `Program.cs` styr vad som händer först.

                        ## Efter veckan kan du
                        - förklara vad `app.UseRouting()` och `app.MapGet()` gör
                        - skriva en endpoint som tar emot en route-parameter
                        - svara med rätt statuskod: `200`, `201`, `400` och `404`

                        ```csharp
                        app.MapGet("/books/{id:int}", (int id, BookStore store) =>
                            store.Find(id) is { } book ? Results.Ok(book) : Results.NotFound());
                        ```

                        Läs kapitel 2 och 3 i kursboken före lektionen på torsdag.
                        """),
                    new DemoAssignment(
                        "Inlämning 1: Minimal API för en bokhylla",
                        """
                        Bygg ett Minimal API för en bokhylla. Böckerna får ligga i minnet den här veckan.

                        - [ ] GET /books listar alla böcker
                        - [ ] GET /books/{id} svarar 404 när boken saknas
                        - [ ] POST /books validerar titel och författare och svarar 201

                        **För VG:** lägg till sökning med `?author=` och minst tre enhetstester för valideringen.

                        Skriv kort hur du har löst uppgiften och vad som var svårast.
                        """,
                        "Jag har byggt API:et med tre endpoints och en BookStore-klass i minnet. Valideringen ligger "
                        + "i en egen metod så att den går att testa. Svårast var att få rätt statuskod när "
                        + "titeln saknas.",
                        14,
                        28,
                        HandIn: DemoHandIn.Repo),
                ]),
            new DemoWeek(
                "Vecka 2: Data med Entity Framework Core",
                "Bokhyllan flyttar från minnet till en riktig databas.",
                21,
                [
                    new DemoTheory(
                        "DbContext, migrationer och relationer",
                        """
                        Entity Framework Core översätter dina C#-klasser till tabeller. `DbContext` är din väg in
                        till databasen, och en **migration** beskriver hur schemat ändras.

                        ## Viktiga kommandon
                        ```bash
                        dotnet ef migrations add AddAuthors
                        dotnet ef database update
                        ```

                        En bok har en författare och en författare har många böcker. Det är en **en-till-många**-
                        relation, och EF Core sätter främmande nyckeln åt dig.
                        """),
                    new DemoAssignment(
                        "Inlämning 2: Bokhyllan får en databas",
                        """
                        Byt ut listan i minnet mot EF Core och SQLite.

                        - [ ] en DbContext med Books och Authors
                        - [ ] minst en migration i repot
                        - [ ] alla endpoints från inlämning 1 fungerar som förut

                        **För VG:** visa författaren med sina böcker utan N+1-frågor.
                        """,
                        "Jag har lagt till en BookContext med två tabeller och en migration. Böckerna hämtas med "
                        + "Include så att författaren kommer med i samma fråga. Alla gamla endpoints fungerar.",
                        35,
                        49,
                        HandIn: DemoHandIn.Repo),
                ]),
            new DemoWeek(
                "Vecka 3: Razor Pages och slutprojekt",
                "Ett gränssnitt för bokklubben och kursens slutprojekt.",
                42,
                [
                    new DemoTheory(
                        "Formulär och validering i Razor Pages",
                        """
                        Razor Pages kopplar ett formulär till en `PageModel`. Med `[BindProperty]` och
                        data annotations som `[Required]` får du validering både i webbläsaren och på servern.

                        Kontrollera alltid `ModelState.IsValid` innan du sparar.
                        """),
                    new DemoAssignment(
                        "Slutprojekt: Bokklubbens webbapp",
                        """
                        Bygg en webbapp där bokklubbens medlemmar kan lägga till böcker och skriva recensioner.

                        - [ ] lista, skapa och ändra böcker
                        - [ ] recensioner med betyg 1–5
                        - [ ] validering i alla formulär

                        **För VG:** inloggning så att bara den som skrev en recension kan ändra den, och en kort
                        reflektion över din arkitektur.
                        """,
                        "Appen har sidor för böcker och recensioner, med validering i alla formulär. Jag har lagt "
                        + "till inloggning med Identity, och bara den som skrev en recension kan ändra den.",
                        63,
                        null,
                        HandIn: DemoHandIn.Repo),
                ]),
        ]);

    private static DemoCourse DevOps => new(
        "DevOps",
        "DevOps",
        "Versionshantering, tester, CI/CD och driftsättning i ett större grupprojekt.",
        30,
        0,
        [
            new DemoWeek(
                "Vecka 1: Git och pull requests",
                "Ett gemensamt arbetssätt för gruppen.",
                0,
                [
                    new DemoTheory(
                        "Branch-strategi och pull requests",
                        """
                        I ett grupprojekt ska `main` alltid gå att driftsätta. Allt arbete sker i egna grenar och
                        kommer in via en **pull request** som någon annan granskar.

                        ## Efter veckan kan du
                        - skapa en gren per issue och namnge den efter issuet
                        - skriva commit-meddelanden enligt Conventional Commits (`feat:`, `fix:`, `docs:`)
                        - sätta upp grenskydd så att ingen kan pusha direkt till `main`
                        """),
                    new DemoAssignment(
                        "Inlämning 1: Skydda main-grenen",
                        """
                        Sätt upp gruppens repo så att `main` är skyddad.

                        - [ ] grenskydd på main med krav på pull request
                        - [ ] minst en granskning innan merge
                        - [ ] en README med hur man kommer igång

                        **För VG:** en mall för pull requests som länkar till ett issue.
                        """,
                        "Vi har grenskydd på main som kräver en pull request och en godkänd granskning. README:n "
                        + "beskriver hur man startar projektet lokalt, och vi har en mall för pull requests.",
                        12,
                        26,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 2: Automatiska tester",
                "Tester som fångar fel innan de når main.",
                14,
                [
                    new DemoTheory(
                        "Testpyramiden och enhetstester i xUnit",
                        """
                        Testpyramiden säger: många snabba **enhetstester**, färre **integrationstester** och bara
                        några få **E2E-tester**.

                        ```csharp
                        [Fact]
                        public void Title_IsRequired() =>
                            Assert.False(Book.Create("", "Astrid Lindgren").IsValid);
                        ```

                        Ett bra test har ett tydligt namn, testar en sak och går lika bra varje gång.
                        """),
                    new DemoAssignment(
                        "Inlämning 2: Testsvit som körs i CI",
                        """
                        Skriv tester för backend och frontend och låt dem köras på varje pull request.

                        - [ ] enhetstester för backend och frontend
                        - [ ] testerna körs automatiskt på pull requests
                        - [ ] en pull request med fallerande tester kan inte mergas

                        **För VG:** visa kodtäckningen i pull requesten.
                        """,
                        "Backend har xUnit-tester och frontend Vitest. Båda körs i ett workflow på varje pull "
                        + "request, och grenskyddet kräver att de går igenom innan merge.",
                        30,
                        null,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 3: CI med GitHub Actions",
                "Bygg, testa och kontrollera formatet automatiskt.",
                35,
                [
                    new DemoTheory(
                        "Workflows, jobb och secrets",
                        """
                        Ett **workflow** i `.github/workflows/` startar på en händelse, till exempel en pull request.
                        Det består av **jobb**, och varje jobb har **steg**.

                        Lösenord och nycklar läggs som **secrets** i GitHub och läses med `${{ secrets.NAMN }}`.
                        Skriv aldrig en nyckel direkt i en fil i repot.
                        """),
                    new DemoAssignment(
                        "Inlämning 3: Pipeline för pull requests",
                        """
                        Bygg en pipeline som stoppar dålig kod innan den når `main`.

                        - [ ] bygget får inte ha några varningar
                        - [ ] formatet kontrolleras automatiskt
                        - [ ] testerna från inlämning 2 körs

                        **För VG:** schemalagda E2E-tester varje natt.
                        """,
                        "Vår pipeline bygger med varningar som fel, kör dotnet format och alla tester. E2E-testerna "
                        + "körs varje natt med ett schemalagt workflow.",
                        50,
                        74,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 4: Containrar och driftsättning",
                "Från en image på din dator till en app i molnet.",
                55,
                [
                    new DemoTheory(
                        "Docker: images och containrar",
                        """
                        En **image** är en färdig förpackning av appen. En **container** är en image som körs.

                        ```dockerfile
                        FROM mcr.microsoft.com/dotnet/aspnet:10.0
                        COPY ./publish /app
                        ENTRYPOINT ["dotnet", "/app/Backend.dll"]
                        ```

                        Bygg imagen i flera steg, så att SDK:n inte följer med till produktion.
                        """),
                    new DemoTheory(
                        "Driftsättning till molnet",
                        """
                        När imagen är byggd kan en molntjänst köra den. Pipelinen bygger imagen, laddar upp den till
                        ett register och säger åt molntjänsten att hämta den nya versionen.

                        Läs igenom checklistan innan du börjar på inlämning 4:
                        - [ ] appen läser all konfiguration från miljövariabler
                        - [ ] databasens lösenord ligger som secret
                        - [ ] appen har en health-endpoint
                        """,
                        BlockedBy: "Docker: images och containrar"),
                    new DemoAssignment(
                        "Inlämning 4: Driftsätt appen",
                        """
                        Driftsätt gruppens app automatiskt när en pull request mergas till `main`.

                        - [ ] en Dockerfile som bygger appen i flera steg
                        - [ ] pipelinen driftsätter automatiskt efter merge till main
                        - [ ] hemligheter ligger som GitHub secrets

                        **För VG:** appen visar sin version, så att man ser vilken release som körs.

                        Skriv länken till den driftsatta appen och en kort beskrivning av flödet i README:n.
                        """,
                        "Appen byggs med en Dockerfile i två steg. Efter merge till main bygger pipelinen imagen och "
                        + "driftsätter den automatiskt. Versionen syns på /version.",
                        70,
                        null,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 5: Release och semantisk versionering",
                "Versionsnummer som sätts av sig själva.",
                72,
                [
                    new DemoTheory(
                        "Semantisk versionering med Conventional Commits",
                        """
                        Med semantisk versionering betyder `2.4.1` major, minor och patch. Commit-meddelandena
                        avgör nästa version: `fix:` ger en patch, `feat:` en minor och `BREAKING CHANGE` en major.
                        """),
                    new DemoAssignment(
                        "Inlämning 5: Automatisk release",
                        """
                        Låt pipelinen sätta versionen och skapa en release vid varje merge till `main`.

                        - [ ] versionen sätts automatiskt från commit-meddelandena
                        - [ ] varje release får en git-tagg
                        """,
                        "Vi använder semantic-release. Varje merge till main ger en ny tagg och en release.",
                        77,
                        null,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
        ],
        [
            new DemoNews(
                "Gästföreläsning om GitOps på torsdag",
                """
                På torsdag kl. 13 kommer en driftingenjör från ett konsultbolag och berättar hur de arbetar med
                GitOps. Ta med frågor om din egen pipeline. Föreläsningen spelas inte in.
                """,
                2),
            new DemoNews(
                "Handledningen flyttas till fredag",
                """
                Den här veckan har vi handledning på fredag kl. 9–12 i stället för onsdag. Boka en tid i kalendern om
                ni vill gå igenom er pipeline.
                """,
                9),
        ]);

    private static DemoCourse Lia => new(
        "LIA",
        "LIA",
        "Lärande i arbete på ett företag, med rapporter till skolan under perioden.",
        60,
        70,
        [
            new DemoWeek(
                "Vecka 1: Inför LIA",
                "Det här behöver vara klart innan du börjar på företaget.",
                0,
                [
                    new DemoTheory(
                        "Så fungerar LIA-perioden",
                        """
                        Under LIA arbetar du på ett företag och har en handledare där. Du skickar en kort rapport till
                        skolan varje vecka, och vi har ett trepartssamtal i mitten av perioden.
                        """),
                    new DemoAssignment(
                        "LIA-plan",
                        "Fyll i mallen i bilagan med din handledare: företaget, handledaren och tre saker du vill lära "
                        + "dig. Ladda upp den underskrivna planen som PDF eller foto.",
                        "",
                        7,
                        null,
                        HandIn: DemoHandIn.File,
                        Scale: GradeScale.PassFail,
                        Attachments: ["lia-plan-mall.pdf"]),
                ]),
            new DemoWeek(
                "Vecka 2: Första veckan på företaget",
                "Den första veckorapporten.",
                7,
                [
                    new DemoAssignment(
                        "Veckorapport 1",
                        "Vad har du gjort den här veckan, vad har du lärt dig och vad vill du ha hjälp med?",
                        "",
                        14,
                        null,
                        Scale: GradeScale.PassFail),
                ]),
            new DemoWeek(
                "Vecka 5: Trepartssamtal",
                "Halvtid på LIA.",
                28,
                [
                    new DemoAssignment(
                        "Trepartssamtal med handledaren",
                        """
                        Du, din handledare och läraren går igenom hur LIA går. Läraren bedömer samtalet direkt
                        efteråt, och du lämnar inte in något här.

                        Läraren bedömer att:
                        - du kan beskriva vad du har gjort och lärt dig
                        - du har en plan för resten av perioden
                        """,
                        "",
                        35,
                        null,
                        HandIn: DemoHandIn.OnSite,
                        Scale: GradeScale.PassFail),
                ]),
        ]);

    // --- JAVA25 ---------------------------------------------------------------------------------------------------

    private static DemoCourse Java => new(
        "OPJ",
        "Objektorienterad programmering med Java",
        "Grunderna i Java: typer, metoder, klasser och första konsolprogrammet.",
        35,
        70,
        [
            new DemoWeek(
                "Vecka 1: Typer och metoder",
                "Variabler, typer och dina första metoder.",
                0,
                [
                    new DemoTheory(
                        "Variabler, typer och metoder i Java",
                        "Java har primitiva typer som `int` och `double` och referenstyper som `String`. En metod har "
                        + "en returtyp, ett namn och parametrar: `static double toFahrenheit(double celsius)`."),
                    new DemoAssignment(
                        "Inlämning 1: Temperaturomvandlaren",
                        "Skriv ett konsolprogram som omvandlar mellan Celsius och Fahrenheit. Ogiltig inmatning ska ge "
                        + "ett tydligt felmeddelande.",
                        "Programmet frågar efter en temperatur och en enhet och skriver ut svaret. Fel inmatning fångas "
                        + "med try/catch.",
                        14,
                        28,
                        HandIn: DemoHandIn.Repo),
                ]),
            new DemoWeek(
                "Vecka 2: Klasser och objekt",
                "Inkapsling och konstruktorer.",
                28,
                [
                    new DemoTheory(
                        "Klasser, konstruktorer och inkapsling",
                        "En klass samlar data och beteende. Gör fälten `private` och låt metoderna se till att "
                        + "objektet alltid är giltigt."),
                    new DemoAssignment(
                        "Inlämning 2: Bankkonto med klasser",
                        "Skapa en klass `Account` med insättning och uttag. Saldot får aldrig bli negativt.",
                        "Account har ett privat saldo. Uttag som är större än saldot ger ett undantag.",
                        49,
                        null,
                        HandIn: DemoHandIn.Repo),
                ]),
        ]);

    private static DemoCourse SpringBoot => new(
        "SpBo",
        "Webbtjänster med Spring Boot",
        "Eleverna bygger en REST-tjänst med Spring Boot och JPA i en större gemensam uppgift.",
        40,
        0,
        [
            new DemoWeek(
                "Vecka 1: Spring Boot från start",
                "Starters, beans och den första controllern.",
                0,
                [
                    new DemoTheory(
                        "Starters, beans och dependency injection",
                        "Spring Boot skapar dina klasser åt dig. Märk en klass med `@Service` och be om den i "
                        + "konstruktorn, så får du den injicerad."),
                    new DemoAssignment(
                        "Inlämning 1: Hello REST",
                        "Skapa en controller med `GET /hello/{name}` som svarar med JSON.",
                        "Controllern svarar med ett JSON-objekt med en hälsning. Jag har ett test med MockMvc.",
                        12,
                        26,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 2: Data med JPA",
                "Entiteter och repositories.",
                14,
                [
                    new DemoTheory(
                        "Entiteter och repositories",
                        "Märk en klass med `@Entity` och skapa ett interface som ärver `JpaRepository`. Spring skriver "
                        + "frågorna åt dig."),
                    new DemoAssignment(
                        "Inlämning 2: Bibliotekets databas",
                        "Spara böcker och lån i en databas med JPA. Ett lån hör till en bok.",
                        "Book och Loan är entiteter med en relation. Repositoryt hittar alla lån för en bok.",
                        30,
                        null,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 3: Validering och felhantering",
                "Ett API som svarar tydligt när något är fel.",
                35,
                [
                    new DemoTheory(
                        "Bean Validation och @ControllerAdvice",
                        "Med `@Valid` och `@NotBlank` stoppas felaktiga data innan de når din kod. En "
                        + "`@ControllerAdvice` gör om undantag till tydliga felsvar."),
                    new DemoAssignment(
                        "Inlämning 3: Robust API",
                        "Lägg till validering och felhantering i bibliotekets API.",
                        "Alla DTO:er har validering, och fel ger 400 med en lista över vad som saknas.",
                        50,
                        74,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
            new DemoWeek(
                "Vecka 4: Säkerhet",
                "Inloggning och behörighet.",
                55,
                [
                    new DemoTheory(
                        "Spring Security i korthet",
                        "Spring Security lägger ett filter framför dina controllers. Bestäm vilka adresser som är "
                        + "öppna och vilka som kräver inloggning."),
                    new DemoAssignment(
                        "Inlämning 4: Inloggning med JWT",
                        "Skydda API:et så att bara inloggade användare kan låna böcker.",
                        "Inloggningen ger en JWT, och lån kräver en giltig token.",
                        70,
                        null,
                        HandIn: DemoHandIn.GroupRepo),
                ]),
        ],
        [
            new DemoNews(
                "Kodgranskning i par på måndag",
                "På måndag granskar ni varandras API i par. Ha er senaste version pushad innan lektionen.",
                3),
        ]);

    private static DemoCourse Testing => new(
        "MjTe",
        "Mjukvarutestning",
        "Enhetstester, integrationstester och testautomatisering för Spring-tjänsten.",
        30,
        70,
        [
            new DemoWeek(
                "Vecka 1: Enhetstester med JUnit",
                "Snabba tester för en klass i taget.",
                0,
                [
                    new DemoTheory(
                        "JUnit 5 och AssertJ",
                        "Ett test i JUnit 5 är en metod med `@Test`. AssertJ ger läsbara kontroller som "
                        + "`assertThat(balance).isEqualTo(100)`."),
                    new DemoAssignment(
                        "Inlämning 1: Tester för bankkontot",
                        "Skriv tester för klassen Account från kursen i Java.",
                        "",
                        14,
                        null,
                        HandIn: DemoHandIn.Repo),
                ]),
            new DemoWeek(
                "Vecka 2: Integrationstester",
                "Tester mot en riktig databas.",
                21,
                [
                    new DemoAssignment(
                        "Inlämning 2: Integrationstester för API:et",
                        "Testa bibliotekets API mot en riktig databas med Testcontainers.",
                        "",
                        35,
                        null,
                        HandIn: DemoHandIn.Repo),
                ]),
        ]);

    // --- FIBER25 --------------------------------------------------------------------------------------------------
    // En yrkesutbildning utan programmering: inlämningar som filer och foton, ett praktiskt prov som bedöms på plats,
    // textsvar för beräkningar och en övning med bara feedback.

    private static DemoCourse FiberTech => new(
        "FiTe",
        "Fiberteknik",
        "Fiberns uppbyggnad, skarvning och mätning med OTDR.",
        30,
        70,
        [
            new DemoWeek(
                "Vecka 1: Fiberns uppbyggnad",
                "Singelmod, multimod och dämpning.",
                0,
                [
                    new DemoTheory(
                        "Singelmod, multimod och dämpning",
                        """
                        En optisk fiber består av en **kärna** och en **mantel**. Ljuset hålls kvar i kärnan för att
                        manteln har lägre brytningsindex.

                        ## Efter veckan kan du
                        - förklara skillnaden mellan singelmod (9 µm kärna) och multimod (50 µm kärna)
                        - räkna ut dämpningen för en länk
                        - välja våglängd: 1310 nm eller 1550 nm

                        ## Typiska värden
                        | Del | Dämpning |
                        |---|---|
                        | Singelmod vid 1550 nm | 0,35 dB/km |
                        | Svetsskarv | högst 0,1 dB |
                        | Kontakt | högst 0,5 dB |

                        Dämpningsbudgeten är summan av allt på länken. Lägg alltid till en marginal på 3 dB.
                        """),
                    new DemoAssignment(
                        "Inlämning 1: Dämpningsbudget för en länk",
                        """
                        Räkna ut dämpningsbudgeten för en länk på 12 km med fyra skarvar och två kontakter, vid
                        1550 nm. Utrustningen klarar 10 dB.

                        - [ ] dämpningen för fibern, skarvarna och kontakterna var för sig
                        - [ ] summan och marginalen
                        - [ ] svar på om länken klarar sig

                        **För VG:** visa hur många skarvar till länken klarar innan marginalen tar slut.

                        Skriv uträkningen i textfältet.
                        """,
                        "Fibern ger 12 × 0,35 = 4,2 dB, skarvarna 4 × 0,1 = 0,4 dB och kontakterna 2 × 0,5 = 1,0 dB. "
                        + "Totalt 5,6 dB. Med 3 dB marginal blir det 8,6 dB, så länken klarar sig inom 10 dB.",
                        14,
                        28),
                ]),
            new DemoWeek(
                "Vecka 2: Skarvning och OTDR",
                "Skarva och mät.",
                28,
                [
                    new DemoTheory(
                        "Skarvning och mätning med OTDR",
                        """
                        En **OTDR** skickar ljuspulser in i fibern och mäter ljuset som kommer tillbaka. Kurvan visar
                        avståndet längs x-axeln och dämpningen längs y-axeln.

                        - En jämn lutning är fiberns egen dämpning.
                        - Ett litet steg nedåt är en svetsskarv.
                        - En topp följd av ett steg är en kontakt.
                        - En hög topp följd av brus är fiberns slut, eller ett brott.

                        Mät från båda hållen och ta medelvärdet, annars kan en skarv se bättre eller sämre ut än
                        den är.
                        """),
                    new DemoAssignment(
                        "Inlämning 2: Tolka en OTDR-kurva",
                        """
                        Tolka kurvan i bilagan. Ange var skarvarna finns, hur stor dämpning varje skarv har och om
                        någon skarv behöver göras om. Gränsvärdet för en skarv är 0,1 dB.
                        """,
                        "Skarvarna finns vid 2,1 km och 5,8 km. Skarven vid 2,1 km har 0,08 dB och är godkänd. "
                        + "Skarven vid 5,8 km har 0,3 dB och bör göras om. Fibern slutar vid 12 km.",
                        49,
                        null,
                        Attachments: ["otdr-lank-a.png"]),
                    new DemoAssignment(
                        "Praktiskt prov: Svetsskarva och mät",
                        """
                        Provet görs i verkstaden. Du skarvar fyra fibrer med svetsen och mäter dem med OTDR medan
                        läraren tittar på. Läraren sätter betyget på plats, och du lämnar inte in något här.

                        Läraren bedömer att:
                        - fibrerna är rent kapade och rengjorda
                        - alla fyra skarvar har högst 0,1 dB
                        - skarvskydden sitter rätt i kassetten
                        """,
                        "",
                        42,
                        null,
                        HandIn: DemoHandIn.OnSite,
                        Scale: GradeScale.PassFail),
                ]),
        ]);

    private static DemoCourse Network => new(
        "NaUp",
        "Nätuppbyggnad",
        "Eleverna planerar och bygger ett mindre fibernät från nod till fastighet.",
        40,
        0,
        [
            new DemoWeek(
                "Vecka 1: Nätets delar",
                "Från nod till fastighet.",
                0,
                [
                    new DemoTheory(
                        "Från nod till fastighet",
                        """
                        Ett fibernät går från en **nod**, via **kopplingsskåp**, ut till varje fastighet. Från noden
                        går en grov kabel till skåpen, och från skåpen går en tunn kabel till varje hus.

                        ## Efter veckan kan du
                        - rita ett nät med nod, skåp och anslutningar
                        - placera skåpen så att kablarna till husen blir korta
                        - planera för fler kunder än i dag

                        Ett skåp ska ha plats för minst hälften fler fastigheter än det har från början.
                        """),
                    new DemoAssignment(
                        "Inlämning 1: Rita nätet för kvarteret",
                        """
                        Rita ett nät för kvarteret på kartan i bilagan, med nod, skåp och alla 24 fastigheter.

                        - [ ] noden och skåpen är utritade
                        - [ ] varje fastighet är ansluten till ett skåp
                        - [ ] skåpen har plats för fler kunder

                        Rita på papper eller i ett program. Ladda upp ritningen som foto, bild eller PDF.
                        """,
                        "Nätet har en nod i korsningen och två skåp. Varje skåp har plats för tolv fastigheter till.",
                        12,
                        26,
                        HandIn: DemoHandIn.File,
                        Attachments: ["karta-kvarteret.png"],
                        AnswerFiles: ["natskiss-kvarteret.png"]),
                ]),
            new DemoWeek(
                "Vecka 2: Projektering",
                "Material och tidsplan.",
                14,
                [
                    new DemoTheory(
                        "Kanalisation och schaktning",
                        """
                        Kanalisationen är rören som fibern blåses in i. Lägg ett grovt rör i huvudstråket och
                        mikrorör ut till husen.

                        - Begär **ledningsanvisning** innan ni gräver, så att ni inte skadar el eller vatten.
                        - Schakta så lite som möjligt och samordna med andra ledningsägare.
                        - Räkna med 5 % extra rör för böjar och skarvar.
                        """),
                    new DemoAssignment(
                        "Övning: Hur mycket rör går åt?",
                        """
                        Kvarteret är 180 meter långt, och varje hus ligger i snitt 25 meter från skåpet. Hur många
                        meter mikrorör behövs till 24 hus, med 5 % extra?

                        Övningen ger inget betyg. Läraren ger dig feedback på uträkningen.
                        """,
                        "24 × 25 = 600 meter. Med 5 % extra blir det 630 meter mikrorör.",
                        21,
                        null,
                        Scale: GradeScale.None),
                    new DemoAssignment(
                        "Inlämning 2: Materiallista och tidsplan",
                        """
                        Gör en materiallista och en tidsplan för nätet från inlämning 1.

                        - [ ] alla rör, kablar och skåp med antal
                        - [ ] en tidsplan vecka för vecka
                        - [ ] ledningsanvisningen finns med i planen

                        Ladda upp listan och planen som PDF eller Excel.
                        """,
                        "Materiallistan har rör, fiber och skåp. Tidsplanen är sex veckor.",
                        30,
                        null,
                        HandIn: DemoHandIn.File,
                        AnswerFiles: ["materiallista-och-tidsplan.pdf"]),
                ]),
            new DemoWeek(
                "Vecka 3: Installation",
                "Blåsning och skåp.",
                35,
                [
                    new DemoTheory(
                        "Blåsning av fiber och skåp",
                        """
                        Fibern blåses in i röret med tryckluft. Kontrollera först att röret är helt och rent med en
                        provkula.

                        Märk varje fiber i skåpet med skåpets nummer och fiberns nummer, till exempel **B2-07**, så
                        att den går att hitta om tio år. Fyll i installationsprotokollet medan du arbetar, inte efteråt.
                        """),
                    new DemoAssignment(
                        "Inlämning 3: Installationsprotokoll",
                        """
                        Fyll i protokollet i bilagan för ert skåp, med märkning, längd och skarvdämpning för varje
                        fiber. Ladda upp det ifyllda protokollet som PDF eller som foto.
                        """,
                        "Protokollet är ifyllt för alla 24 fibrer, med märkning och längd.",
                        50,
                        74,
                        HandIn: DemoHandIn.File,
                        Attachments: ["installationsprotokoll-mall.pdf"],
                        AnswerFiles: ["installationsprotokoll-skap-2.pdf"]),
                ]),
            new DemoWeek(
                "Vecka 4: Driftsättning och dokumentation",
                "Slutmätning och överlämning.",
                55,
                [
                    new DemoTheory(
                        "Slutmätning och dokumentation",
                        """
                        Innan nätet lämnas över mäts varje fiber från båda hållen. Mätvärdena sparas i
                        dokumentationen tillsammans med ritningarna.

                        Överlämningsdokumentet ska innehålla:
                        - [ ] en ritning över nätet som det faktiskt byggdes
                        - [ ] mätvärden för varje fiber
                        - [ ] installationsprotokollen för alla skåp
                        - [ ] kontaktuppgifter till den som har byggt nätet
                        """),
                    new DemoAssignment(
                        "Inlämning 4: Överlämningsdokument",
                        """
                        Skriv överlämningsdokumentet för nätet, med mätvärden och ritningar. Följ checklistan i
                        teorin för veckan.

                        **För VG:** en kort analys av vad ni skulle göra annorlunda nästa gång.

                        Ladda upp dokumentet som PDF.
                        """,
                        "Dokumentet har ritningar, mätvärden för alla fibrer och en lista över skåpen.",
                        70,
                        null,
                        HandIn: DemoHandIn.File),
                ]),
        ],
        [
            new DemoNews(
                "Fältdag på onsdag",
                """
                På onsdag är vi ute på fältet hela dagen och blåser fiber i kvarteret. Ta med arbetskläder,
                skyddsskor och varselväst. Vi samlas vid skåp 2 kl. 7.30.
                """,
                4),
            new DemoNews(
                "Svetsen i verkstaden är lagad",
                "Svetsen fungerar igen. Ni som inte hann göra det praktiska provet kan boka en ny tid i kalendern.",
                12),
        ]);

    private static DemoCourse FieldProject => new(
        "FaPr",
        "Fältprojekt",
        "Ett projekt ute på fält där eleverna driftsätter och dokumenterar ett nät.",
        45,
        84,
        [
            new DemoWeek(
                "Vecka 1: Projektstart",
                "Säkerhet och riskanalys.",
                0,
                [
                    new DemoTheory(
                        "Säkerhet på arbetsplatsen",
                        """
                        Gå igenom riskerna innan arbetet börjar. Ingen arbetar ensam i en schakt, och arbete nära
                        väg kräver en godkänd trafikanordningsplan.
                        """),
                    new DemoAssignment(
                        "Inlämning 1: Riskanalys",
                        "Gör en riskanalys för projektet i mallen i bilagan och ladda upp den.",
                        "",
                        14,
                        null,
                        HandIn: DemoHandIn.File,
                        Attachments: ["riskanalys-mall.pdf"]),
                ]),
            new DemoWeek(
                "Vecka 2: Fältarbete",
                "Arbetet ute på fältet.",
                21,
                [
                    new DemoAssignment(
                        "Inlämning 2: Dagbok från fältet",
                        "Skriv vad du har gjort varje dag och vad som gick fel.",
                        "",
                        35,
                        null),
                ]),
            new DemoWeek(
                "Vecka 3: Slutbesiktning",
                "Nätet besiktas tillsammans med beställaren.",
                49,
                [
                    new DemoAssignment(
                        "Slutbesiktning på plats",
                        """
                        Läraren och beställaren går igenom nätet med er på plats och bedömer arbetet där. Ni lämnar
                        inte in något här.
                        """,
                        "",
                        63,
                        null,
                        HandIn: DemoHandIn.OnSite,
                        Scale: GradeScale.PassFail),
                ]),
        ]);
}

// Hur eleven lämnar in. Repo och GroupRepo blir GitHub-uppgifter när skolan har GitHub kopplat och ett mallrepo
// finns, annars text och filer. GroupRepo ger ett repo per projektgrupp. File tar emot filer och en kommentar.
// OnSite bedöms av läraren i klassrummet, och eleven lämnar inte in något.
internal enum DemoHandIn
{
    Text,
    Repo,
    GroupRepo,
    File,
    OnSite
}

// FeaturedStudent är inloggningen som får en demoväg i klassen, och HelpMessage frågan som en annan elev redan har
// ställt på den öppna uppgiften.
internal sealed record DemoClass(
    DemoCourse Completed,
    DemoCourse Current,
    DemoCourse Upcoming,
    int CurrentDaysLeft,
    string? FeaturedStudent = null,
    string? HelpMessage = null);

// Days är kursens längd i dagar. Den pågående kursens slut styrs av klassen i stället (0 här).
internal sealed record DemoCourse(
    string CodeSuffix,
    string Title,
    string Description,
    int Points,
    int Days,
    DemoWeek[] Weeks,
    DemoNews[]? News = null);

// OpensOnDay är dagen då veckan och allt i den publiceras, räknat från kursens första dag.
internal sealed record DemoWeek(string Title, string Description, int OpensOnDay, DemoPart[] Parts);

internal abstract record DemoPart(string Title, string Body);

// BlockedBy är titeln på en annan del i samma vecka som eleven måste bli klar med först.
internal sealed record DemoTheory(string Title, string Body, string? BlockedBy = null) : DemoPart(Title, Body);

// Answer är elevernas svar i inlämningen, tomt för uppgifter i kurser som inte har börjat. ResubmitOnDay är
// kompletteringsdeadline, eller null när uppgiften bara har en deadline. Attachments är lärarens bilagor och
// AnswerFiles filerna som eleverna har lämnat in, båda filnamn i DemoFiles. Scale None är en övning med bara feedback.
internal sealed record DemoAssignment(
    string Title,
    string Body,
    string Answer,
    int DueOnDay,
    int? ResubmitOnDay,
    DemoHandIn HandIn = DemoHandIn.Text,
    GradeScale Scale = GradeScale.PassFailDistinction,
    string[]? Attachments = null,
    string[]? AnswerFiles = null) : DemoPart(Title, Body);

// DaysAgo är hur många dagar sedan nyheten publicerades.
internal sealed record DemoNews(string Title, string Body, int DaysAgo);
