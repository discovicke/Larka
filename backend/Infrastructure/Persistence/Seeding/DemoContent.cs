namespace Backend.Infrastructure.Persistence.Seeding;

/// <summary>
/// Texterna i demodatan: tre klasser med en avslutad, en pågående och en kommande kurs. Dagarna räknas från kursens
/// första dag. DemoSeed lägger kurserna så att den pågående kursen har en vecka öppen just nu, med en uppgift
/// vars deadline ligger mer än en vecka fram, så att demon går att visa några dagar efter att datan skapats.
/// </summary>
internal static class DemoContent
{
    /// <summary>Uppgiften som blir en GitHub-uppgift när Database:DemoTemplateRepo är satt.</summary>
    public const string GitHubAssignmentTitle = "Inlämning 4: Driftsätt appen";

    public const string GitHubCourseCode = ".NET25_DevOps";

    public static readonly IReadOnlyDictionary<string, DemoClass> Classes = new Dictionary<string, DemoClass>
    {
        [".NET25"] = new(WebApp, DevOps, Lia, CurrentDaysLeft: 18),
        ["JAVA25"] = new(Java, SpringBoot, Testing, CurrentDaysLeft: 35),
        ["FIBER25"] = new(FiberTech, Network, FieldProject, CurrentDaysLeft: 50),
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
                        28),
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
                        49),
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
                        null),
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
                        26),
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
                        null),
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
                        74),
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
                        GitHubAssignmentTitle,
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
                        GitHub: true),
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
                        null),
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
                        "Beskriv vilket företag du är på, vem som är din handledare och tre saker du vill lära dig.",
                        "",
                        7,
                        null),
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
                        null),
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
                        28),
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
                        null),
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
                        26),
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
                        null),
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
                        74),
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
                        null),
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
                        null),
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
                        null),
                ]),
        ]);

    // --- FIBER25 --------------------------------------------------------------------------------------------------

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
                        "Singelmodsfiber används för långa avstånd. Dämpningen mäts i dB och ökar med längden, "
                        + "skarvarna och kontakterna."),
                    new DemoAssignment(
                        "Inlämning 1: Dämpningsbudget för en länk",
                        "Räkna ut dämpningsbudgeten för en länk på 12 km med fyra skarvar och två kontakter.",
                        "Fibern ger 4,2 dB, skarvarna 0,4 dB och kontakterna 1,0 dB. Totalt 5,6 dB, inom budgeten.",
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
                        "En OTDR skickar ljuspulser och mäter det som reflekteras. Kurvan visar var skarvar, "
                        + "kontakter och brott finns."),
                    new DemoAssignment(
                        "Inlämning 2: Tolka en OTDR-kurva",
                        "Tolka kurvan i bilagan. Ange var skarvarna finns och om någon skarv är för dålig.",
                        "Skarvarna finns vid 2,1 km och 5,8 km. Skarven vid 5,8 km har 0,3 dB och bör göras om.",
                        49,
                        null),
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
                        "Ett fibernät går från en nod, via kopplingsskåp, till varje fastighet. Planera för fler "
                        + "kunder än i dag."),
                    new DemoAssignment(
                        "Inlämning 1: Rita nätet för kvarteret",
                        "Rita ett nät för kvarteret på kartan i bilagan, med nod, skåp och fastigheter.",
                        "Nätet har en nod och två skåp. Varje skåp har plats för tolv fastigheter till.",
                        12,
                        26),
                ]),
            new DemoWeek(
                "Vecka 2: Projektering",
                "Material och tidsplan.",
                14,
                [
                    new DemoTheory(
                        "Kanalisation och schaktning",
                        "Kanalisationen är rören som fibern blåses i. Schakta så lite som möjligt och samordna med "
                        + "andra ledningsägare."),
                    new DemoAssignment(
                        "Inlämning 2: Materiallista och tidsplan",
                        "Gör en materiallista och en tidsplan för nätet från inlämning 1.",
                        "Materiallistan har rör, fiber och skåp. Tidsplanen är sex veckor.",
                        30,
                        null),
                ]),
            new DemoWeek(
                "Vecka 3: Installation",
                "Blåsning och skåp.",
                35,
                [
                    new DemoTheory(
                        "Blåsning av fiber och skåp",
                        "Fibern blåses in i röret med tryckluft. Märk varje fiber så att den går att hitta i skåpet."),
                    new DemoAssignment(
                        "Inlämning 3: Installationsprotokoll",
                        "Fyll i installationsprotokollet för ert skåp.",
                        "Protokollet är ifyllt för alla 24 fibrer, med märkning och längd.",
                        50,
                        74),
                ]),
            new DemoWeek(
                "Vecka 4: Driftsättning och dokumentation",
                "Slutmätning och överlämning.",
                55,
                [
                    new DemoTheory(
                        "Slutmätning och dokumentation",
                        "Innan nätet lämnas över mäts varje fiber. Mätvärdena sparas i dokumentationen."),
                    new DemoAssignment(
                        "Inlämning 4: Överlämningsdokument",
                        "Skriv överlämningsdokumentet för nätet, med mätvärden och ritningar.",
                        "Dokumentet har ritningar, mätvärden för alla fibrer och en lista över skåpen.",
                        70,
                        null),
                ]),
        ],
        [
            new DemoNews(
                "Fältdag på onsdag",
                "På onsdag är vi ute på fältet hela dagen. Ta med arbetskläder och skyddsskor.",
                4),
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
                        "Gå igenom riskerna innan arbetet börjar. Ingen arbetar ensam i en schakt."),
                    new DemoAssignment(
                        "Inlämning 1: Riskanalys",
                        "Gör en riskanalys för projektet.",
                        "",
                        14,
                        null),
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
        ]);
}

internal sealed record DemoClass(DemoCourse Completed, DemoCourse Current, DemoCourse Upcoming, int CurrentDaysLeft);

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
// kompletteringsdeadline, eller null när uppgiften bara har en deadline. GitHub betyder att uppgiften kan bli en
// repo-uppgift, och då har ingen elev lämnat in text i förväg.
internal sealed record DemoAssignment(
    string Title,
    string Body,
    string Answer,
    int DueOnDay,
    int? ResubmitOnDay,
    bool GitHub = false) : DemoPart(Title, Body);

// DaysAgo är hur många dagar sedan nyheten publicerades.
internal sealed record DemoNews(string Title, string Body, int DaysAgo);
