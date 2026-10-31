using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

internal static partial class DemoCatalog
{
    // --- DevOps: den pågående kursen (och samma kurs, avslutad, för äldre klasser) ---

    private static CourseSpec DevOps(string prefix, DateOnly firstMonday, bool current, string author)
    {
        var end = firstMonday.AddDays(32);
        // Labbarna och veckorna bytte rubrik i oktober 2026. Was pekar på de gamla rubrikerna så att prod hittar dem.
        static string? OldLabTitle(string title) => title switch
        {
            "Inlämning: Git-flöde i teamet" => "Git-flöde i teamet",
            "Laboration: CI/CD" => "CI-pipeline för er backend",
            "Laboration: Docker och containrar" => "Containerisera applikationen",
            "Laboration: Testautomation i CI" => "Testautomation i CI",
            "Laboration: Release och driftsättning" => "Releasa och driftsätt",
            _ => null
        };

        // Veckorna numrerades först efter kalendervecka (42–46), nu efter kursvecka.
        static ModuleSpec[] RenumberWeeks(ModuleSpec[] weeks) =>
        [
            .. weeks.Select((week, index) => week with
            {
                Was = week.Was ?? $"Vecka {index + 42}{week.Title[week.Title.IndexOf(' ', 6)..]}"
            })
        ];

        PartSpec DevOpsTask(int dayOffset, GradeScale scale, string title, string body, SubmissionPattern pattern,
            string[] answers, string[] feedback, string[] needsWork, string? was = null) => Assignment(
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
            needsWork,
            was ?? OldLabTitle(title));

        // Veckornas publicering: v45 och v46 öppnas på fredagen veckan innan, som läraren brukar göra.
        DateTimeOffset? WeekOpen(int week) => current && week >= 3
            ? Se(firstMonday.AddDays((week * 7) - 3), 12)
            : Se(firstMonday.AddDays(week * 7), 7);

        return new(
            $".{prefix}_DEVOPS",
            "DevOps",
            "Versionshantering, kontinuerlig integration, containrar, automatiserade tester och driftsättning i "
            + "molnet. Vi arbetar i team och släpper kod hela vägen från commit till produktion.\n\n"
            + "**Kursmål**\n\n1. Att arbeta i ett Git-flöde med pull requests och kodgranskning.\n"
            + "2. Att bygga och testa automatiskt med GitHub Actions.\n"
            + "3. Att paketera en applikation i en container.\n"
            + "4. Att skriva automatiska tester på flera nivåer och köra dem i en pipeline.\n"
            + "5. Att versionera och släppa en tjänst och driftsätta den i molnet.\n"
            + "6. Att samarbeta i team och redovisa tekniska val.\n"
            + "7. Att arbeta med versionshantering, test, deploy och releasehantering.",
            25,
            firstMonday,
            end,
            author,
            RenumberWeeks(
            [
                new(
                    "Vecka 1 – Git-flöden och samarbete",
                    "Hur ett team delar upp arbetet i branchar och granskar varandras kod.",
                    WeekOpen(0),
                    92,
                    [
                        Lesson(
                            "Teori: Git-flöden i team",
                            """
                            Git i sig säger ingenting om hur ett team ska arbeta. Det finns ingen inbyggd regel om vem
                            som får pusha vart, hur branchar ska heta eller när något är klart nog att ligga på `main`.
                            Det är därför man behöver ett **Git-flöde**: en överenskommelse i teamet om hur kod tar sig
                            från någons dator till produktion. Utan en sådan överenskommelse brukar det sluta med att
                            tre personer jobbat i samma fil, att `main` är trasig på fredag eftermiddag och att
                            ingen riktigt vet vilken version som körs.

                            De tre flöden man oftast hör talas om är **GitHub Flow**, **GitFlow** och **trunk-based
                            development**. I GitHub Flow skapar man en kortlivad branch från `main`, gör sin ändring,
                            öppnar en pull request, får den granskad och mergar tillbaka. Därefter driftsätts `main`.
                            GitFlow har i stället långlivade branchar som `develop` och `release/*`, och passar bäst
                            för produkter som släpps sällan och i fasta versioner, till exempel ett skrivbordsprogram
                            eller ett bibliotek med flera supportade versioner. Trunk-based går åt andra hållet: alla
                            jobbar mot `main` med väldigt små ändringar, och halvfärdigt arbete göms bakom
                            **feature flags** tills det är redo.

                            Vilket som är bäst beror på hur ofta ni släpper och hur ni testar. Har ni bra automatiska
                            tester och släpper flera gånger i veckan är GitHub Flow eller trunk-based nästan alltid
                            enklast. GitFlow betyder mer merge-arbete och fler chanser till konflikter, och de flesta
                            team vi träffar har valt bort det. Den viktigaste egenskapen hos ett flöde är att `main`
                            alltid går att bygga, testa och släppa.

                            Branchnamn kan låta som en detalj, men de sparar tid. Ett vanligt mönster är
                            `typ/ärendenummer-kort-beskrivning`, till exempel `feature/123-login` eller
                            `fix/88-datumformat`. Då syns direkt vad branchen gäller, och man kan länka den till rätt
                            ärende utan att fråga.

                            ## Git-flöden i praktiken

                            Anta att ni är fem personer som bygger en webbtjänst och har valt GitHub Flow. Du tar
                            ärende 123, "Lägg till inloggning", och börjar med att hämta senaste `main`:

                            ```bash
                            git switch main
                            git pull
                            git switch -c feature/123-login
                            ```

                            Du jobbar, committar ofta och pushar branchen. När du är nöjd öppnar du en pull request
                            mot `main`. Pipelinen startar av sig själv och bygger och testar koden. Medan den kör
                            tittar en kollega på ändringen, ställer två frågor och godkänner. När allt är grönt
                            mergar du, branchen tas bort och nästa arbetsflöde tar vid och bygger en ny version.

                            Det här låter självklart, men det är först när alla gör likadant som det börjar
                            fungera. Därför ska ni skriva ned flödet i repots README redan den första dagen. Det tar
                            tio minuter och räddar många diskussioner senare.
                            """,
                            "Git-flöden: GitHub Flow, GitFlow och trunk-based"),
                        Lesson(
                            "Teori: Pull requests och kodgranskning",
                            """
                            En pull request är mer än ett sätt att få in kod. Den är ett samtal om koden, och ofta
                            det enda tillfälle då en kollega faktiskt läser det du skrivit. Därför lönar det sig att
                            göra den lätt att granska. En bra pull request är **liten**, har en tydlig beskrivning
                            och går att förstå på tio minuter. Är ändringen 1 500 rader brukar granskaren skumma, och
                            då missar man precis det man ville fånga.

                            Beskrivningen ska säga *varför* ändringen görs. Vad som ändrats syns i diffen, men
                            bakgrunden syns inte. Länka gärna ärendet med `Closes #123`, så stängs det automatiskt när
                            pull requesten mergas, och lägg till en skärmdump om något ser annorlunda ut. Det är också
                            fint att berätta vad du *inte* gjort, eller var du är osäker och vill ha en extra titt.

                            Som granskare är det lätt att bli för snäll eller för bestämd. Fråga hellre än beordra
                            ("Varför valde du en lista här och inte en mängd?") och skilj på det som måste ändras
                            och det som är en smaksak. Många team prefixar kommentarer med `nit:` när det är det
                            senare. Godkänn först när du själv förstått koden, inte när du tror att den är rätt.

                            ## Pull requests i praktiken

                            Här är en beskrivning som är lätt att granska:

                            ```markdown
                            ## Varför
                            Användare loggas ut efter en minut eftersom token förnyas på fel ställe.

                            ## Vad
                            - Flyttar förnyelsen till `AuthService`
                            - Lägger till test för utgången token

                            Closes #88
                            ```

                            Tänk också på att granskningen är ett lagarbete. Svarar du inte på en granskares fråga
                            inom en dag får hen fråga igen, och då flyter inget. Lika viktigt: tacka för bra
                            kommentarer. Det kostar ingenting.
                            """,
                            "Pull requests och kodgranskning"),
                        Lesson(
                            "Teori: Branch protection och CODEOWNERS",
                            """
                            Ett flöde som bara bygger på att alla kommer ihåg reglerna håller tills någon har
                            bråttom. **Branch protection** gör reglerna till teknik: GitHub vägrar helt enkelt att
                            ta emot en push som bryter mot dem. Man skyddar oftast `main`, och de vanligaste
                            inställningarna är:

                            - kräv en pull request innan merge, och minst en godkänd granskning
                            - kräv att vissa **status checks** (alltså pipelinens jobb) är gröna
                            - kräv att branchen är uppdaterad mot `main` innan merge
                            - förbjud force-push och borttagning av branchen

                            Det du ställer in hittar du under *Settings → Branches*, eller som en **ruleset** som kan
                            gälla flera repon. Var noga med namnet på status checken: den måste matcha jobbnamnet i
                            workflowet, annars väntar GitHub på en check som aldrig kommer och pull requesten blir
                            för alltid "pending".

                            Med filen `CODEOWNERS` går det att ange vem som ska granska vilken del av koden. GitHub
                            lägger då till rätt personer automatiskt, och du kan kräva att en kodägare godkänner.

                            ```text
                            # .github/CODEOWNERS
                            /backend/           @org/backend-team
                            /client/            @org/frontend-team
                            /.github/workflows/ @org/devops
                            ```

                            ## Skydd i praktiken

                            Skyddet gäller också dig själv, även om du är administratör. Det kan kännas krångligt
                            första gången du vill rätta ett stavfel direkt på `main`, men det är precis så det ska
                            vara: reglerna ska gälla alla, annars slutar de gälla någon. Tänk på att skyddet först
                            blir användbart när pipelinen finns, för det är den som är det första kravet. Vi tar
                            nästa steg nästa vecka.
                            """,
                            "Branch protection och CODEOWNERS"),
                        Reading(
                            "Läs: The Phoenix Project, kapitel 1–4",
                            """
                            Läs kapitel 1–4 i The Phoenix Project. Boken är en roman om en IT-chef som hamnar mitt i
                            en kris, och den beskriver ovanligt träffsäkert hur det ser ut när utveckling och drift
                            inte pratar med varandra.

                            Anteckna **tre saker** du känner igen från ert eget projekt, eller från en tidigare
                            arbetsplats. Det kan vara en flaskhals, ett missförstånd eller något som alltid går fel
                            på fredagar. Vi pratar om dem på torsdagens lektion, så ta med anteckningarna.

                            Klarmarkera när du läst.
                            """,
                            Se(firstMonday.AddDays(4), 23, 59)),
                        Exercise(
                            "Övningsuppgift: Lös en merge-konflikt",
                            """
                            För att öva på att reda ut konflikter i ett säkert sammanhang ska ni göra en med flit. Det
                            är mycket mindre obehagligt att göra det första gången när inget står på spel.

                            Skapa ett nytt, tomt repo med en fil `hälsning.txt` som innehåller en rad text, och gör
                            sedan så här:

                            ```bash
                            git switch -c ändra-a
                            # ändra raden i hälsning.txt, committa
                            git switch main
                            git switch -c ändra-b
                            # ändra SAMMA rad på ett annat sätt, committa
                            git switch ändra-a
                            git merge ändra-b
                            ```

                            Git kommer att stanna och säga att det finns en konflikt. Öppna filen och leta efter
                            markeringarna `<<<<<<<`, `=======` och `>>>>>>>`. Bestäm vad som ska gälla, ta bort
                            markeringarna, kör `git add` och slutför med `git commit`.

                            Gör sedan om övningen, men låt den här gången en kamrat ändra den andra branchen i ett
                            gemensamt repo, och lös konflikten i en pull request på GitHub. Notera vad som var
                            enklast i terminalen respektive i webbgränssnittet.

                            Ni lämnar inte in något. Klarmarkera när du löst båda varianterna.
                            """),
                        DevOpsTask(
                            11,
                            GradeScale.PassFail,
                            "Inlämning: Git-flöde i teamet",
                            """
                            Välj ett Git-flöde för ert projekt och beskriv det i en kort text. Redovisningen ska
                            kunna läsas av en ny kollega som ska börja i teamet nästa vecka.

                            Texten ska innehålla:

                            - vilket flöde ni valt (GitHub Flow, trunk-based eller något annat) och varför ni valt
                              bort de andra
                            - hur ni namnger branchar och commits
                            - hur ni granskar: vem, hur snabbt och vad granskaren ska titta efter
                            - vad som krävs för att merga, och hur ni skyddat `main` med branch protection

                            ## Att lämna in

                            - [ ] En text på en halv till en hel sida
                            - [ ] En länk till inställningarna för branch protection i ert repo (skärmdump räcker)

                            **Betygskriterier**

                            Uppgiften är godkänd när redovisningen visar att ni har gjort ett medvetet val och att
                            reglerna är ==skyddade av teknik, inte bara av goda föresatser==. Den knyter an till
                            kursmål 1, att arbeta i ett Git-flöde med pull requests och kodgranskning.
                            """,
                            SubmissionPattern.MostlyGraded,
                            [
                                "Vi valde GitHub Flow: `main` är alltid driftsättningsbar och allt arbete sker i "
                                + "korta feature-branchar med namn som `feature/123-login`. Varje pull request "
                                + "granskas av minst en kollega, och branch protection kräver grön CI. GitFlow "
                                + "kändes för tungt för ett team på fem personer som släpper ofta.",
                                "Vi kör trunk-based med mycket små pull requests. Vi har feature flags för det som "
                                + "inte är klart och slår ihop till `main` flera gånger om dagen. Granskning sker "
                                + "inom en timme enligt vår teamöverenskommelse.",
                                "GitHub Flow, med skillnaden att vi har en `release`-tagg per vecka. Branchar heter "
                                + "`typ/ärende-kort-beskrivning` och kräver en granskare. CODEOWNERS pekar ut vem "
                                + "som granskar backend respektive klient.",
                                "Vi har valt GitHub Flow. Branchar heter `feature/` eller `fix/` plus ärendenummer. "
                                + "Vi mergar med squash så att historiken blir ren, och force-push är avstängt på "
                                + "`main`. Skärmdump på inställningarna ligger i repots wiki."
                            ],
                            [
                                "Tydligt resonemang om varför ni valde bort GitFlow. Skärmdumpen av branch protection "
                                + "visar att ni krävt både granskning och grön check. Godkänt.",
                                "Bra att ni skrivit ned hur snabbt en granskning ska ske. Det är den regel som oftast "
                                + "glöms bort. Godkänt.",
                                "Välgjort, och CODEOWNERS är ett bra tillägg. Lägg gärna till vem som är reserv när "
                                + "ägaren är sjuk. Godkänt."
                            ],
                            [
                                "Ni beskriver flödet, men inte hur `main` skyddas. Lägg till en skärmdump av "
                                + "branch protection och lämna in igen.",
                                "Jag saknar motivering till varför ni valde bort de andra flödena. Utveckla det med "
                                + "två, tre meningar.",
                                "Texten säger att alla granskar, men inställningen kräver ingen godkänd review. "
                                + "Rätta antingen texten eller inställningen och lämna in igen."
                            ])
                    ]),
                new(
                    "Vecka 2 – Kontinuerlig integration med GitHub Actions",
                    "Bygg, testa och kontrollera formatering automatiskt vid varje ändring.",
                    WeekOpen(1),
                    88,
                    [
                        Lesson(
                            "Teori: DevOps och CI/CD",
                            """
                            DevOps, som är en sammanslagning av orden Development och Operations, kan lite förenklat
                            beskrivas som ett arbetssätt där utveckling, drift och kvalitetssäkring samarbetar för att
                            leverera programvara snabbt, säkert och tillförlitligt. Tidigare satt de ofta i olika
                            avdelningar: utvecklarna skrev koden, testarna testade den och en driftgrupp tog emot den
                            och hoppades på det bästa. Varje överlämning tog tid och skapade missförstånd.

                            En viktig del av DevOps är att automatisera återkommande moment. Det som görs för hand
                            varje gång, som att bygga, köra tester och kopiera filer till en server, glöms bort och
                            görs olika av olika personer. Det som körs av en maskin görs likadant varje gång och går
                            att granska i efterhand. De här automatiska stegen samlas i en **pipeline**, ofta kallad
                            **CI/CD**.

                            **CI**, Continuous Integration, innebär att utvecklarna integrerar sina ändringar ofta
                            och att varje ändring bygger och testas automatiskt. Då upptäcks fel samma dag som de
                            skrivs, inte tre veckor senare. **CD** kan stå för Continuous Delivery, där varje
                            godkänd ändring *kan* släppas med ett knapptryck, eller Continuous Deployment, där den
                            släpps helt automatiskt. Skillnaden är om en människa trycker på knappen.

                            En pipeline körs på en **agent**, ofta kallad **runner**: en maskin som hämtar koden,
                            kör stegen och rapporterar tillbaka. Hos GitHub hyr man en färdig maskin per körning,
                            men man kan också ha egna. Poängen är att körningen alltid startar från ett rent läge,
                            så att "det fungerar på min dator" inte räknas som ett svar.

                            Det som kvalitetssäkras behöver inte vara bara tester. Det går också bra att kontrollera
                            formatering, stavning i dokumentation, säkerhetshål i beroenden och att koden går att
                            bygga för flera plattformar.

                            ## Hur DevOps kan se ut i praktiken

                            Tänk dig ett team som arbetar enligt GitHub Flow. Du skapar en branch och gör din
                            ändring. När du öppnar en pull request startar en pipeline som bygger koden, kör
                            testerna och kontrollerar formateringen. Resultatet visas direkt i pull requesten, och
                            branch protection hindrar merge så länge något är rött.

                            En kollega granskar koden och godkänner. Du mergar, och då startar ett nytt workflow på
                            `main`. Det bygger en release-version, paketerar den och lägger den som artefakt eller
                            som en image i ett register. Därifrån driftsätts den antingen automatiskt, eller manuellt
                            när någon godkänt det i en miljö som heter något i stil med `production`.

                            Allt det här går att följa i GitHub: vem som ändrade vad, vilket test som föll och
                            vilken version som körs. Test-statusen på huvudgrenen brukar även följas noggrant. Om den
                            blir röd släpper teamet det de gör och rättar, eller backar ändringen. Ofta för att IT
                            kommer och ropar på oss.
                            """,
                            "Så fungerar GitHub Actions"),
                        Lesson(
                            "Teori: Workflows, jobb och steg",
                            """
                            I GitHub Actions beskriver man en pipeline i en YAML-fil under `.github/workflows/`. En
                            fil är ett **workflow**. Det startas av en eller flera **händelser** (`on:`), till
                            exempel `push`, `pull_request`, ett schema eller en manuell knapp (`workflow_dispatch`).
                            Ett workflow innehåller ett eller flera **jobb** som körs parallellt om de inte får
                            ett beroende av varandra med `needs:`. Varje jobb består av **steg**, och ett steg är
                            antingen ett skalkommando (`run:`) eller en färdig åtgärd (`uses:`).

                            ```yaml
                            name: ci
                            on:
                              push:
                                branches: [main]
                              pull_request:
                            jobs:
                              build:
                                runs-on: ubuntu-latest
                                steps:
                                  - uses: actions/checkout@v4
                                  - uses: actions/setup-dotnet@v4
                                    with:
                                      dotnet-version: 10.0.x
                                  - run: dotnet format --verify-no-changes
                                  - run: dotnet build -warnaserror
                                  - run: dotnet test
                            ```

                            Två saker går ofta fel första gången. Den första är indenteringen i YAML: ett steg som
                            hamnar en nivå fel blir ett annat steg eller ett fel. Den andra är att `uses:` hämtar
                            kod från någon annan, så pinna version med `@v4` och granska vad ni kör.

                            ## Workflows i praktiken

                            Ett vanligt mönster är ett workflow för pull requests och ett annat för release. Det
                            första är snabbt och körs ofta, det andra är noggrant och körs sällan. Ge jobben namn som
                            säger vad de gör (`format`, `test-backend`, `test-client`), eftersom det är dem ni
                            pekar ut i branch protection.
                            """,
                            "Matrisbyggen, cache och artefakter"),
                        Lesson(
                            "Teori: Secrets, miljöer och cache",
                            """
                            Lägg aldrig lösenord, nycklar eller anslutningssträngar i koden, inte ens i ett privat
                            repo. De ligger kvar i historiken även om du tar bort dem senare. Använd i stället
                            **secrets**: krypterade värden som lagras i repot eller i en **miljö**, och som ett
                            workflow kan läsa med `${{ secrets.NAMN }}`.

                            ```yaml
                            deploy:
                              runs-on: ubuntu-latest
                              environment: production
                              steps:
                                - run: ./deploy.sh
                                  env:
                                    API_KEY: ${{ secrets.API_KEY }}
                            ```

                            En miljö (`environment: production`) kan kräva att en människa godkänner innan jobbet
                            körs, och kan ha egna secrets. Det är ett enkelt sätt att få en spärr före produktion.
                            Hemligheter maskas i loggen, men skriv dem aldrig med `echo` ändå.

                            För att pipelinen ska gå snabbt kan ni cacha beroenden med `actions/cache` eller
                            den inbyggda cachen i `actions/setup-node`. Spara resultat som **artefakter**
                            (`actions/upload-artifact`) om ett senare jobb eller en människa behöver dem. En
                            **matris** kör samma jobb på flera versioner, till exempel .NET 9 och 10.

                            ## Säkerhet i praktiken

                            Pull requests från forkar får inte läsa era secrets, med goda skäl. Tänk på det om ni
                            någon gång tar emot bidrag utifrån, och aktivera `permissions:` så snävt som möjligt i
                            varje workflow.
                            """,
                            "Secrets och miljöer"),
                        DevOpsTask(
                            16,
                            GradeScale.PassFailDistinction,
                            "Laboration: CI/CD",
                            """
                            I den här laborationen får ni ett litet repo med en .NET-del och en JavaScript-del.
                            Koden fungerar i stort sett, men den saknar både tester och automatisering. Er uppgift
                            är att lägga till en pipeline, skydda `main` och skriva tester så att allt som
                            beskrivs nedan uppfylls.

                            Ni väljer själva vilka verktyg som ska användas för byggen, formatering och tester.
                            Valfria testramverk får användas, men xUnit och Jest rekommenderas.

                            Projektet ska konfigureras så att följande uppfylls:

                            - Branchen `main` är skyddad
                              - en pull request krävs, och minst en godkänd granskning
                              - alla status checks från pipelinen måste vara gröna före merge
                              - force-push och borttagning är förbjudet
                            - Ett workflow körs vid varje pull request och vid push till `main`
                              - koden byggs, och kompilatorns varningar räknas som fel
                              - testerna körs, både för .NET (`dotnet test`) och JavaScript (`npm test`)
                              - formateringen kontrolleras (`dotnet format --verify-no-changes` och Prettier)
                            - Ett andra workflow skapar en release när en tagg som `v1.0.0` pushas
                              - bygget paketeras och läggs som bilaga på en GitHub Release
                              - release notes genereras automatiskt
                            - Driftsättning sker manuellt via `workflow_dispatch` till en miljö som heter
                              `staging`

                            För att visa att det fungerar ska det finnas ==öppna pull requests som blockeras av
                            pipelinen==. Använd **en PR per punkt** i listan ovan: en där formateringen är fel, en
                            där ett test fallerar, en där bygget varnar, och så vidare. Man ska alltså i er
                            pull request-lista kunna se varje krav uppfyllas, och varje krav brytas.

                            Testerna ska täcka funktionerna `prepareTitle` och `createTaskBody` i JavaScript-delen,
                            samt motsvarande klass `TaskBodyBuilder` i .NET-delen. Funktionerna ska testas med olika
                            värden, liksom ibland ogiltiga eller tomma värden. Tänk på vad som ska hända med en
                            titel som är `null`, bara blanksteg eller 500 tecken lång.

                            ## 💡 Tips

                            Den befintliga koden är inte perfekt. Du kommer att hitta saker som du vill ändra, och
                            ibland upptäcka att ett test visar att koden inte gör det du trodde. Rätta det i en
                            separat pull request, så att granskaren inte behöver gissa vad som hör till vad.

                            Formateringsreglerna bestämmer ni själva. Det viktiga är att pipelinen och era
                            editorer är överens, så lägg in `.editorconfig` och `.prettierrc` i repot. Då slipper
                            ni den klassiska situationen där ett bygge faller på ett blanksteg.

                            ## Att lämna in

                            - [ ] Länk till repot med både workflows och testerna
                            - [ ] Länkar till de öppna pull requests som visar varje krav (en per punkt)
                            - [ ] Skärmdump eller export av inställningarna för branch protection
                            - [ ] En kort text om hur ni hanterar secrets och cache

                            **Betygskriterier**

                            För godkänt ska alla punkter ovan vara uppfyllda och visas upp. För väl godkänt ska ni
                            dessutom ha cachat beroenden, kört testerna i en matris och redovisat hur ni hanterar
                            secrets och behörigheter. Laborationen knyter an till kursmål 7: Att arbeta med
                            versionshantering, test, deploy och releasehantering.
                            """,
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
                                + "något vid uppgradering. Pull requests kan inte mergas förrän båda är gröna.",
                                "Jest-testerna för `prepareTitle` täcker tom sträng, bara blanksteg och en titel på "
                                + "500 tecken. Vi hade en PR där `npx prettier --check` föll, och en annan som "
                                + "bröts av ett test. Båda ligger öppna i repot som vi länkat.",
                                "Vi har gjort allt utom release-workflowet. Taggen byggs men bilagan laddas inte "
                                + "upp än. Resten är uppfyllt och vi har sex öppna PRs som visar kraven."
                            ],
                            [
                                "Pipelinen bygger och testar, bra. Lägg till cache för NuGet så går bygget snabbare.",
                                "Tydligt upplagt och väl redovisat. Alla sex PRs visar sitt krav, och testen för "
                                + "tomma titlar i `prepareTitle` är bra. Godkänt.",
                                "Bra: ni har även en matris för flera .NET-versioner, ett `staging`-jobb som kräver "
                                + "godkännande och hanterar secrets på rätt sätt. Väl godkänt."
                            ],
                            [
                                "Jag saknar en redovisning av hur ni hanterar secrets. Komplettera det och lämna in "
                                + "igen.",
                                "Nästan godkänt: workflowet saknar tester för frontend. Lägg till `npm test` och "
                                + "lämna in igen.",
                                "Pipelinen bygger men kör inte testerna, och `main` är inte skyddad med "
                                + "status checks. Lägg till teststeget och visa en PR som blockeras av ett fel."
                            ])
                    ], true),
                new(
                    "Vecka 3 – Docker och containrar",
                    "Paketera applikationen så att den körs likadant överallt.",
                    WeekOpen(2),
                    75,
                    [
                        Lesson(
                            "Teori: Docker i korthet",
                            """
                            "Det fungerar på min dator" är en av programmeringens äldsta ursäkter. Skillnaden mellan
                            din dator och servern kan vara operativsystemet, .NET-versionen, en miljövariabel eller
                            en fil som du glömde checka in. Med **containrar** paketerar man applikationen
                            tillsammans med allt den behöver, och kör det paketet likadant överallt.

                            En **image** är en mall: en fryst samling filer och inställningar. En **container** är
                            en körande instans av en image, ungefär som ett objekt är en instans av en klass. Du
                            kan starta tio containrar från samma image, och de påverkar inte varandra. En container
                            är inte en virtuell maskin. Den delar värdens kärna och startar därför på någon sekund.

                            Images byggs i **lager**, ett per instruktion i en `Dockerfile`. Lagren cachas, och
                            om ett lager är oförändrat återanvänds det. Det betyder att ordningen spelar roll:
                            kopiera projektfilerna och återställ beroenden *före* du kopierar resten av koden, så
                            ändras det dyra lagret bara när beroendena ändras.

                            ```bash
                            docker build -t larka:dev .
                            docker run --rm -p 8080:8080 larka:dev
                            docker ps
                            docker logs <container>
                            ```

                            Data i en container försvinner när den tas bort. Det som ska överleva, som databasens
                            filer, läggs i en **volym**. Inställningar skickas in som miljövariabler och inte
                            bakas in i imagen.

                            ## Docker i praktiken

                            I ert projekt bygger pipelinen en image för varje release, taggar den med versionen och
                            skickar den till ett register, som GitHub Container Registry. Servern hämtar sedan exakt
                            den imagen. Det som testats är då det som körs, och att backa är bara att starta
                            föregående tagg.
                            """,
                            "Docker i korthet: images, lager och containrar"),
                        Lesson(
                            "Teori: Dockerfile och multi-stage build",
                            """
                            En `Dockerfile` är en steg-för-steg-beskrivning av hur imagen byggs. För ett
                            .NET-projekt behövs SDK:n för att bygga, men inte för att köra. Med en **multi-stage
                            build** bygger du i en stor image och kopierar bara resultatet till en liten:

                            ```dockerfile
                            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
                            WORKDIR /src
                            COPY *.sln ./
                            COPY backend/*.csproj backend/
                            RUN dotnet restore
                            COPY . .
                            RUN dotnet publish backend -c Release -o /app

                            FROM mcr.microsoft.com/dotnet/aspnet:10.0
                            WORKDIR /app
                            COPY --from=build /app .
                            USER $APP_UID
                            ENTRYPOINT ["dotnet", "backend.dll"]
                            ```

                            Den färdiga imagen innehåller ingen SDK och blir mycket mindre, ofta en femtedel. Den
                            innehåller dessutom inga byggverktyg som en angripare kan använda. Raden `USER` gör att
                            processen inte körs som root, vilket är en billig säkerhetsvinst.

                            Lägg också en `.dockerignore` bredvid, med `bin`, `obj`, `node_modules` och `.git`.
                            Annars kopieras allt det in i bygget och gör det långsamt, och ibland används gamla
                            byggfiler i stället för nya.

                            ## Imagen i praktiken

                            Kör `docker images` och titta på storleken. Om den är över 500 MB är det nästan alltid
                            något som kommit med av misstag. `docker history` visar vilket lager som är dyrast.
                            """,
                            "Dockerfile med multi-stage build"),
                        Lesson(
                            "Teori: docker compose för utveckling",
                            """
                            De flesta applikationer består av mer än en process: en webbtjänst, en databas och
                            kanske en kö. Med `docker compose` beskriver du alltihop i en fil och startar det med
                            ett kommando, `docker compose up`. Alla nya i teamet kan då komma igång på en kvart.

                            ```yaml
                            services:
                              db:
                                image: postgres:17
                                environment:
                                  POSTGRES_PASSWORD: dev
                                volumes:
                                  - dbdata:/var/lib/postgresql/data
                                healthcheck:
                                  test: ["CMD", "pg_isready", "-U", "postgres"]
                                  interval: 5s
                              app:
                                build: .
                                ports: ["8080:8080"]
                                depends_on:
                                  db:
                                    condition: service_healthy
                            volumes:
                              dbdata:
                            ```

                            Två saker är värda att lägga märke till. **Volymen** gör att databasen överlever att
                            containern startas om. **Healthchecken** gör att appen väntar på att databasen är
                            redo, så att den inte kraschar vid start för att Postgres behöver tre sekunder.

                            ## Compose i praktiken

                            Compose används främst för utveckling och test, inte för produktion. Samma
                            `docker-compose.yml` är däremot bra att köra i pipelinen för att ge testerna en riktig
                            databas.
                            """,
                            "docker compose för utveckling"),
                        DevOpsTask(
                            18,
                            GradeScale.PassFailDistinction,
                            "Laboration: Docker och containrar",
                            """
                            I den här laborationen ska ni paketera er applikation som en container. Ni utgår från
                            det repo ni redan har, och bygger vidare på pipelinen från förra veckan.

                            Ni väljer själva vilken bas-image ni använder, men den ska vara en officiell och ha en
                            tydlig version (inte `latest`).

                            Projektet ska konfigureras så att följande uppfylls:

                            - En `Dockerfile` finns i repots rot
                              - den använder multi-stage build, där bygget sker i SDK-imagen
                              - den körs inte som root
                              - beroenden återställs i ett eget lager före koden kopieras
                            - En `.dockerignore` utesluter åtminstone `bin`, `obj`, `node_modules` och `.git`
                            - En `docker-compose.yml` startar applikationen tillsammans med databasen
                              - databasen har en volym och en `healthcheck`
                              - appen väntar på att databasen är redo
                              - lösenord läses från en `.env`-fil som inte är incheckad
                            - Pipelinen bygger imagen vid varje pull request
                              - vid en tagg publiceras imagen till GitHub Container Registry
                              - taggen på imagen är samma som version på releasen

                            För att visa att det fungerar ska ni kunna köra `docker compose up` på en ren dator och
                            nå applikationen på port 8080. Redovisa ==imagens storlek före och efter== att ni
                            införde multi-stage och `.dockerignore`, samt vad som gjorde skillnaden. Använd
                            `docker images` och `docker history` för att visa det.

                            Ni ska också visa vad som händer när något går fel: stoppa databasen och beskriv hur
                            appen beter sig, och hur compose startar om den.

                            ## 💡 Tips

                            Bygg imagen lokalt först, och lägg inte in den i pipelinen förrän den fungerar. En
                            pipeline som bygger en trasig image tar flera minuter per försök, medan ett lokalt
                            bygge med cache tar sekunder.

                            Det är lätt att råka baka in en hemlighet i en image, till exempel via `COPY . .`. Titta
                            med `docker history` att inget sådant följt med, och rotera nyckeln om det gjort det.

                            ## Att lämna in

                            - [ ] Länk till repot med `Dockerfile`, `.dockerignore` och `docker-compose.yml`
                            - [ ] Utdrag från `docker images` som visar storleken före och efter
                            - [ ] En kort text om vad ni gjort för att hålla imagen liten och säker

                            **Betygskriterier**

                            För godkänt ska en fungerande multi-stage `Dockerfile` och en compose-fil finnas och
                            kunna köras. För väl godkänt ska imagen dessutom publiceras av pipelinen vid en tagg,
                            köras som icke-root och redovisningen visa mätbara förbättringar. Laborationen knyter
                            an till kursmål 3, att paketera en applikation i en container.
                            """,
                            SubmissionPattern.Early,
                            [
                                "Vi har en multi-stage `Dockerfile` där bygget sker i SDK-imagen och körningen i "
                                + "`aspnet`. Imagen blev 215 MB. Compose startar appen och Postgres med en volym och "
                                + "en healthcheck.",
                                "Dockerfile med multi-stage och en `.dockerignore` som utesluter `bin`, `obj` och "
                                + "`node_modules`. Imagen gick från 1,2 GB till 230 MB. Compose-filen har en "
                                + "separat profil för tester.",
                                "Imagen körs som `$APP_UID` och pipelinen publicerar den till ghcr.io när vi pushar "
                                + "en tagg. Lösenordet läses från `.env`, som ligger i `.gitignore`. Vi testade att "
                                + "stoppa Postgres, och appen startar om sig själv tills healthchecken är grön."
                            ],
                            [
                                "Bra gjort: storleken gick från över en gigabyte till 230 MB och ni kan förklara "
                                + "varför, med `docker history` som stöd. Godkänt.",
                                "Compose-filen är tydlig och healthchecken gör att appen väntar in databasen. Bra att "
                                + "lösenordet ligger i `.env`. Godkänt.",
                                "Mycket bra. Publiceringen till ghcr.io i pipelinen, icke-root och mätningen före och "
                                + "efter lyfter det till väl godkänt."
                            ],
                            [
                                "Din `Dockerfile` kopierar `.` före `dotnet restore`, så beroenden hämtas på nytt "
                                + "vid varje ändring. Ändra ordningen, mät om och lämna in igen.",
                                "Jag ser ingen `.dockerignore`, och imagen är 1,4 GB. Lägg till filen, bygg om och "
                                + "redovisa storleken före och efter.",
                                "Compose startar appen men den kraschar första gången eftersom databasen inte är "
                                + "redo. Lägg till `healthcheck` och `depends_on` med `service_healthy`."
                            ])
                    ], true),
                new(
                    "Vecka 4 – Automatiska tester i pipelinen",
                    "Testpyramiden, integrationstester och end-to-end.",
                    WeekOpen(3),
                    25,
                    [
                        Lesson(
                            "Teori: Testpyramiden och vad som ska testas",
                            """
                            Automatiska tester är det som gör att man törs ändra i koden. Utan dem är varje ändring
                            ett litet spel, och med dem kan en pipeline säga ja eller nej inom några minuter. Men
                            det går att ha för många, för långsamma eller för dåliga tester, och då blir de
                            en börda i stället för ett stöd.

                            **Testpyramiden** är en tumregel för hur testerna bör fördelas. Längst ned finns många
                            snabba **enhetstester** som kollar en funktion eller klass i isolering. I mitten finns
                            färre **integrationstester** som kollar att delar fungerar ihop, till exempel att ett
                            API kan spara till en riktig databas. Överst finns ett fåtal **end-to-end-tester** som
                            styr hela programmet så som en användare gör. Ju högre upp, desto längre tid tar de och
                            desto oftare går de sönder av helt andra orsaker.

                            Testa *beteende*, inte implementation. Ett test som går sönder av en omstrukturering,
                            när inget faktiskt är fel, är ett dåligt test. Fråga dig vad användaren eller anropande
                            kod förväntar sig, och testa det. Testa också kanterna: tomma värden, `null`, mycket långa
                            texter, negativa tal och datum runt månadsskiften.

                            Ett bra test har tre delar, ofta kallade *arrange, act, assert*: ställ in läget, gör
                            något, kontrollera resultatet. Ger du testet ett namn som säger vad som förväntas, till
                            exempel `PrepareTitle_TrimsWhitespace`, förstår nästa person direkt vad som gått fel när
                            det blir rött.

                            ## Tester i praktiken

                            Täckningsgrad (coverage) är ett hjälpmedel och inget mål. 100 procent säger att varje rad
                            körts, inte att något kontrollerats. Använd den för att hitta det ni glömt, och bry er
                            mer om att de viktigaste flödena har tester än om siffran.
                            """,
                            "Testpyramiden och vad som ska testas"),
                        Lesson(
                            "Teori: Integrationstester med Testcontainers",
                            """
                            Många fel uppstår först när koden möter en riktig databas: en kolumn med fel typ, en
                            migrering som aldrig kördes eller en fråga som fungerar i ett minnesbaserat test men inte
                            i Postgres. Därför är det bra att testa mot samma databas som i produktion i stället
                            för en låtsasversion.

                            **Testcontainers** är ett bibliotek som startar en container, till exempel Postgres,
                            åt testerna och släcker den efteråt. Varje testkörning får en ren databas, och det krävs
                            ingenting installerat förutom Docker.

                            ```csharp
                            public sealed class DatabaseFixture : IAsyncLifetime
                            {
                                private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

                                public string ConnectionString => _postgres.GetConnectionString();

                                public Task InitializeAsync() => _postgres.StartAsync();

                                public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
                            }
                            ```

                            Starta containern en gång per testklass eller testsamling och inte per test, annars
                            tar sviten en evighet. Rensa tabellerna mellan testerna, eller kör varje test i en
                            transaktion som rullas tillbaka.

                            ## Integrationstester i praktiken

                            GitHub-runners har Docker installerat, så Testcontainers fungerar i pipelinen utan
                            extra arbete. Räkna med att de tar några sekunder längre än enhetstester, och lägg dem i
                            ett eget jobb så att ni snabbt ser vilken sorts test som fallerat.
                            """,
                            "Integrationstester med Testcontainers"),
                        Lesson(
                            "Teori: End-to-end med Playwright i CI",
                            """
                            Ett end-to-end-test styr programmet utifrån, så som en användare skulle göra: öppnar
                            en sida, loggar in, klickar och kontrollerar vad som visas. **Playwright** styr en
                            riktig webbläsare och vet att vänta på att element dyker upp, vilket gör testerna
                            mycket stabilare än äldre verktyg.

                            ```typescript
                            test("elev kan lämna in en uppgift", async ({ page }) => {
                              await page.goto("/");
                              await page.getByLabel("E-post").fill("elev@demo.larka.se");
                              await page.getByRole("button", { name: "Logga in" }).click();
                              await expect(page.getByText("Mina kurser")).toBeVisible();
                            });
                            ```

                            Använd om möjligt etiketter och roller i stället för CSS-klasser när du pekar ut
                            element, eftersom en omdesign då inte bryter testerna.

                            I pipelinen körs testerna mot en tillfällig miljö, till exempel appen i en container
                            med compose. När ett test fallerar vill man se *varför*, så spara **trace**,
                            skärmdumpar och video som artefakter. Det är skillnaden mellan att rätta ett fel på tio
                            minuter och att försöka reproducera det hela eftermiddagen.

                            ## Instabila tester i praktiken

                            Ett test som ibland går igenom och ibland inte kallas instabilt (*flaky*), och är värre
                            än inget test, eftersom folk slutar lita på pipelinen. Låt dem aldrig ligga röda: ställ
                            dem i karantän, skapa ett ärende och rätta dem inom en vecka. Förbjud `sleep` i
                            testerna. Vänta in det du faktiskt behöver.
                            """,
                            "End-to-end med Playwright i CI"),
                        Exercise(
                            "Övningsuppgift: E2E-terminalprogramstester",
                            """
                            För att öva på end-to-end-tester utan webbläsare ska vi utgå från följande program, en
                            enkel att göra-lista i terminalen:

                            ```text
                            > dotnet run add "Putsa fjädrarna"
                            Lade till uppgift 1: Putsa fjädrarna

                            > dotnet run add "Boka möte"
                            Lade till uppgift 2: Boka möte

                            > dotnet run list
                            1 [ ] Putsa fjädrarna
                            2 [ ] Boka möte

                            > dotnet run done 1
                            Uppgift 1 klar

                            > dotnet run done 99
                            Fel: uppgift 99 finns inte
                            ```

                            Målet är att testa programmet som en helhet: ni ska starta det som en egen process, ge
                            det argument och kontrollera både vad det skriver ut och vilken **exitkod** det ger. Ni
                            ska alltså inte anropa enskilda klasser i koden.

                            Ett rekommenderat sätt är att skriva en liten hjälpfunktion som startar processen med
                            `System.Diagnostics.Process`, läser `stdout` och `stderr` och returnerar dem tillsammans
                            med exitkoden. Det går också att använda ett bibliotek som CliWrap om ni hellre vill det.
                            Välj det ni tycker är enklast att läsa.

                            Skriv tester för att:

                            - en uppgift kan läggas till och syns i listan
                            - flera uppgifter får löpande nummer
                            - en uppgift kan markeras som klar och listan visar det
                            - att klarmarkera en uppgift som inte finns ger ett felmeddelande och en exitkod som
                              inte är 0
                            - ett okänt kommando skriver ut hjälptexten
                            - `add` utan text ger ett fel och ändrar ingenting

                            Säkerställ att varje test körs från grunden, med en egen tom datafil, så att testerna
                            inte påverkar varandra och kan köras i valfri ordning. Det är det vanligaste felet i
                            den här sortens tester.

                            Ni lämnar inte in något. Klarmarkera när testerna går igenom.
                            """),
                        DevOpsTask(
                            25,
                            GradeScale.PassFailDistinction,
                            "Laboration: Testautomation i CI",
                            """
                            I den här laborationen ska ni bygga ut testerna i ert projekt och köra dem i pipelinen.
                            Ni har redan enhetstester från tidigare. Nu ska ni lägga till integrationstester och
                            minst ett end-to-end-test, och se till att pipelinen blir snabb och tillförlitlig.

                            Testramverk väljer ni själva. Vi rekommenderar xUnit och Testcontainers för backend, och
                            Playwright för end-to-end.

                            Projektet ska konfigureras så att följande uppfylls:

                            - Integrationstester körs mot en riktig Postgres
                              - databasen startas av testerna själva (Testcontainers eller compose)
                              - varje test har en ren databas, eller städar efter sig
                              - minst ett test per endpoint som skriver till databasen
                            - Minst ett end-to-end-test körs i pipelinen
                              - det täcker ett flöde, till exempel logga in och lämna in en uppgift
                              - trace och skärmdumpar sparas som artefakter när testet fallerar
                            - Testerna är uppdelade i egna jobb (enhet, integration, e2e)
                              - alla tre är obligatoriska status checks på `main`
                              - jobben körs parallellt där det går
                            - Instabila tester hanteras
                              - ni har en rutin för hur de märks, ställs i karantän och rättas

                            Visa att det fungerar med ==en pull request där ett integrationstest fallerar och
                            blockerar merge==. Ni ska också mäta hur lång tid varje jobb tar, och redovisa vad ni
                            gjort för att korta tiden.

                            Testerna ska täcka både det som ska fungera och det som ska misslyckas. Ett
                            POST-anrop med ogiltig data ska till exempel ge `400` och inte skriva något i databasen.
                            Kontrollera det med en fråga mot databasen efteråt.

                            ## 💡 Tips

                            Börja med att få ett enda integrationstest att gå grönt lokalt och i pipelinen innan du
                            skriver fler. De flesta problem handlar om hur anslutningssträngen når testet, och det är
                            bäst att lösa en gång.

                            Resist the temptation att skriva end-to-end-tester för allt. Ett par bra flöden är mer
                            värda än tjugo bräckliga. Lägg ni energi på att göra dem stabila.

                            ## Att lämna in

                            - [ ] Länk till repot med testerna och pipelinen
                            - [ ] Länk till en körning med alla tre jobb gröna och en PR där ett fallerar
                            - [ ] En text om tider och om hur ni hanterar instabila tester

                            **Betygskriterier**

                            För godkänt ska alla tre testnivåer köras i pipelinen och vara krav för merge. För väl
                            godkänt ska ni dessutom ha mätt och förbättrat körtiden och ha en fungerande rutin för
                            instabila tester. Laborationen knyter an till kursmål 4, att skriva automatiska tester på
                            flera nivåer och köra dem i en pipeline.
                            """,
                            SubmissionPattern.None,
                            [
                                "Backend har 140 enhetstester och 25 integrationstester mot Postgres via "
                                + "Testcontainers. Dessutom ett Playwright-test för inloggningen. Hela sviten tar fyra "
                                + "minuter i pipelinen. Instabila tester sätter vi i karantän och rättar inom en "
                                + "vecka.",
                                "Vi har lagt till integrationstester för alla endpoints och ett end-to-end-test som "
                                + "loggar in och lämnar in en uppgift. Playwright körs i ett eget jobb och sparar "
                                + "trace som artefakt när något går fel.",
                                "Tre jobb: enhet (40 s), integration (1 min 50 s) och e2e (2 min 10 s) som körs "
                                + "parallellt. Vi kortade e2e från fem minuter genom att dela databasen mellan "
                                + "testen och återanvända inloggningen. En PR med ett ogiltigt POST-anrop "
                                + "som ska ge 400 är länkad."
                            ],
                            [
                                "Alla tre jobb är obligatoriska checks och ni visar en PR som blockeras av ett "
                                + "integrationstest. Bra att ni kontrollerar databasen efter 400-svaret. Godkänt.",
                                "Tydlig redovisning av tider före och efter, och trace-filerna som artefakt gjorde "
                                + "det lätt att se varför e2e-testet föll. Väl godkänt.",
                                "Bra rutin för instabila tester med karantän och ärende. Godkänt."
                            ],
                            [
                                "Playwright-testet sparar ingen trace när det fallerar. Lägg till "
                                + "`actions/upload-artifact` med `if: failure()` och lämna in igen.",
                                "Era integrationstester delar databas utan att städa, så ordningen avgör resultatet. "
                                + "Rensa tabellerna mellan testerna eller använd transaktioner.",
                                "Jag ser bara ett jobb med alla tester. Dela upp dem i enhet, integration och e2e, "
                                + "och gör alla tre till obligatoriska checks."
                            ])
                    ]),
                new(
                    "Vecka 5 – Versionering, release och driftsättning",
                    "Från tagg till produktion, och tillbaka igen om det går fel.",
                    WeekOpen(4),
                    0,
                    [
                        Lesson(
                            "Teori: Semantisk versionering och changelog",
                            """
                            Ett versionsnummer är ett löfte till den som använder programmet. Ser jag att du
                            gått från 1.4.2 till 1.4.3 vill jag kunna lita på att inget går sönder när jag
                            uppgraderar. Med **semantisk versionering** får numret just den betydelsen. Det
                            skrivs **MAJOR.MINOR.PATCH**:

                            - höj `MAJOR` när något går sönder för användarna
                            - höj `MINOR` när du lägger till något bakåtkompatibelt
                            - höj `PATCH` för buggrättningar

                            Det ger en bra tumregel: ändrar du formen på ett API-svar är det en ny major, även
                            om ändringen var liten och bra. Det är användarens kod som avgör, inte din.

                            Att skriva versionsnummer och release notes för hand är tråkigt och glöms bort. Med
                            *conventional commits* (`feat:`, `fix:`, `feat!:` för brytande ändring) kan både
                            nästa version och en **changelog** räknas ut automatiskt från historiken. Kräver ni
                            dessutom att pull requestens titel följer formatet, och squashar vid merge, blir varje
                            ändring en rad i changelogen.

                            ```text
                            feat(auth): lägg till inloggning med e-post
                            fix(date): visa rätt veckodag i kalendern
                            feat!: byt namn på fältet "titel" till "rubrik"
                            ```

                            ## Versionering i praktiken

                            Verktyg som `release-please` eller `semantic-release` läser commit-historiken, räknar ut
                            nästa version och skapar en release-PR. När den mergas skapas taggen och en GitHub
                            Release. Då behöver ingen i teamet komma ihåg att höja siffrorna.
                            """,
                            "Semantisk versionering och changelog"),
                        Lesson(
                            "Teori: Release-flöden med taggar och GitHub Releases",
                            """
                            En release är en tagg på en commit som gick igenom hela pipelinen. Taggen är ett
                            permanent namn på just den versionen: går något fel om ett halvår kan du checka ut
                            `v1.2.0` och se exakt vad som kördes.

                            Låt pipelinen skapa tagg, release notes och den image som driftsätts, så att det som
                            testats är exakt det som släpps. Ett vanligt mönster är ett separat workflow som startar
                            på taggar:

                            ```yaml
                            on:
                              push:
                                tags: ["v*.*.*"]
                            jobs:
                              release:
                                runs-on: ubuntu-latest
                                permissions:
                                  contents: write
                                steps:
                                  - uses: actions/checkout@v4
                                  - run: dotnet publish backend -c Release -o out
                                  - uses: softprops/action-gh-release@v2
                                    with:
                                      files: out/**
                                      generate_release_notes: true
                            ```

                            Bygg bara en gång. Om testmiljön och produktion bygger var sin image från samma kod
                            kan de skilja sig åt, och då har ni inte testat det ni släpper. Bygg imagen vid
                            taggen och flytta *samma* image mellan miljöerna.

                            ## Releaser i praktiken

                            Skydda taggarna också. Bara de som får släppa ska kunna skapa `v*`-taggar, och en
                            publicerad tagg flyttas aldrig. Hittar du ett fel i `v1.2.0` släpper du `v1.2.1`.
                            """,
                            "Release-flöden: taggar och GitHub Releases"),
                        Lesson(
                            "Teori: Driftsättning, rollback och övervakning",
                            """
                            Att koden är mergad betyder inte att den är i produktion, och att den är i produktion
                            betyder inte att den fungerar. Driftsätt först till en **testmiljö** (ofta kallad
                            *staging*) som liknar produktion, och kontrollera där. I GitHub Actions modellerar ni
                            miljöerna med `environment:`, där produktionsmiljön kan kräva godkännande av en människa.

                            Ha en plan för **rollback** innan ni släpper, inte efter. Den enklaste planen är att
                            köra den förra versionen igen: starta driftsättningen med föregående tagg. Det
                            fungerar bara om databasmigreringar är bakåtkompatibla, så tänk igenom dem. Lägg till en
                            kolumn först, börja använda den i en senare release och ta bort den gamla i en tredje.

                            Efter releasen övervakar ni. Det finns tre saker man tittar på: **hälsokontroller**
                            (svarar `/health` med 200?), **loggar** (kommer det nya fel?) och **mätvärden** (har
                            svarstiden eller felfrekvensen ändrats?). Sätt **larm** på de viktigaste, så att det är
                            systemet som ropar och inte användarna.

                            ## Driftsättning i praktiken

                            En driftsättning är inte klar förrän du sett att den fungerar. Många team har en fast
                            rutin: släpp, titta på felloggen och hälsokontrollen i tio minuter, och släpp inte
                            något annat under tiden. Släpp helst inte på fredag eftermiddag, om det inte finns
                            någon som kan rätta det.
                            """,
                            "Driftsättning: miljöer, rollback och övervakning"),
                        DevOpsTask(
                            32,
                            GradeScale.PassFailDistinction,
                            "Laboration: Release och driftsättning",
                            """
                            I den sista laborationen ska ni släppa en riktig version av er tjänst, hela vägen från
                            tagg till molnet. Ni bygger på allt ni gjort i de tidigare veckorna.

                            Molnleverantör väljer ni själva. Vi rekommenderar en som har en gratisnivå och som kan
                            köra en container, till exempel Render eller Fly.io.

                            Projektet ska konfigureras så att följande uppfylls:

                            - Versionering följer semantisk versionering
                              - releaser har taggar som `v1.0.0`
                              - en changelog genereras automatiskt av conventional commits
                              - pull requestens titel valideras av pipelinen
                            - Ett release-workflow startar på en tagg
                              - det bygger imagen och publicerar den med taggens version
                              - det skapar en GitHub Release med release notes
                            - Driftsättning sker i två steg
                              - först till `staging`, automatiskt
                              - sedan till `production`, med godkännande av en människa i miljön
                              - hemligheter ligger som secrets i respektive miljö
                            - Tjänsten övervakas efter release
                              - den har en `/health`-endpoint som pipelinen kontrollerar efter driftsättning
                              - ni har bestämt vilka loggar och mätvärden ni tittar på

                            För att visa att det fungerar ska ni släppa minst två versioner. Den andra ska
                            innehålla en ändring som syns för användaren, och ni ska i redovisningen visa
                            ==hur ni rullar tillbaka== till den första. Gör det på riktigt och spara loggen.

                            Beskriv även hur ni hanterar databasmigreringar vid en rollback, och vad ni skulle
                            göra om hälsokontrollen blir röd efter en driftsättning.

                            ## 💡 Tips

                            Sätt upp releaseflödet mot en tom testtjänst först. Det är fånigt att felsöka ett
                            fel i `release.yml` samtidigt som man felsöker applikationen.

                            Läs pipelinens loggar uppifrån och ned. Det första felet är nästan alltid det
                            verkliga, och allt som kommer efter är följdfel.

                            ## Att lämna in

                            - [ ] Länk till repot, till de två releaserna och till de två körningarna
                            - [ ] Adressen till tjänsten (staging eller produktion)
                            - [ ] En text om rollback, migreringar och övervakning

                            **Betygskriterier**

                            För godkänt ska ni ha släppt två versioner via taggar och driftsatt dem, och kunna
                            visa hur en rollback går till. För väl godkänt ska ni dessutom ha godkännande i
                            produktionsmiljön, en automatisk changelog och en kontrollerad hälsokontroll i
                            pipelinen. Laborationen knyter an till kursmål 5 och 7: att versionera och släppa en
                            tjänst, och att arbeta med versionshantering, test, deploy och releasehantering.
                            """,
                            SubmissionPattern.None,
                            [
                                "Version 1.2.0 släpptes via en tagg som pipelinen bygger till en image och "
                                + "driftsätter till testmiljön och sedan produktion. Changelogen genereras av "
                                + "conventional commits. Vid fel rullar vi tillbaka till föregående tagg, och vi "
                                + "övervakar hälsokontrollen och felloggen i tio minuter efter varje release.",
                                "Vi släppte 2.0.0 eftersom ett API-svar ändrade form. Driftsättningen sker med "
                                + "godkännande i miljön production. Rollback gör vi genom att köra det gamla "
                                + "workflowet med föregående tagg. Vi övervakar felfrekvens och svarstid.",
                                "Vi släppte v1.0.0 och v1.1.0 till Render. Release-workflowet kontrollerar `/health` "
                                + "efter varje deploy. Rollback testade vi genom att köra v1.0.0 igen. Migreringen i "
                                + "1.1.0 bara lägger till en kolumn, så den gamla versionen fungerar mot den nya "
                                + "databasen."
                            ],
                            [
                                "Rollbacken är verkligen genomförd och loggen finns med. Bra resonemang om att "
                                + "migreringar måste vara bakåtkompatibla. Godkänt.",
                                "Tydlig kedja från tagg till produktion med godkännande i miljön. Hälsokontrollen i "
                                + "pipelinen gör att ni upptäcker fel direkt. Väl godkänt.",
                                "Changelogen genereras av conventional commits, och PR-titeln valideras. Godkänt."
                            ],
                            [
                                "Ni har bara släppt en version. Släpp en till och visa rollbacken mellan dem.",
                                "Produktionsmiljön saknar godkännande, så varje tagg går rakt ut. Lägg till "
                                + "`required reviewers` på miljön och visa en körning som väntar.",
                                "Rollbacken är beskriven men inte gjord. Utför den och bifoga loggen, och "
                                + "förklara vad som händer med databasen."
                            ])
                    ])
            ]),
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
                new(author, "Ja, ni fortsätter i samma team om ingen har önskemål om byte.", 6),
                new("#12", "Vi är fyra i vårt team sedan Hanna slutade. Kan vi få en femte?", 9),
                new(author, "Jag hör med Karin, så löser vi det i början av veckan. Önska gärna någon särskild.", 11),
                new("#12", "Tack!", 12)
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
                new(author, "Länken räcker som komplement, men skriv ändå en kort redovisning.", 5),
                new("#8", "Måste vi verkligen ha en PR per punkt? Vi har gjort allt i en och samma PR.", 7),
                new(
                    author,
                    "Ja. Poängen är att jag ska kunna se varje krav uppfyllas och brytas, och det går inte om allt "
                    + "ligger i en PR som redan är mergad. Öppna nya, små PRs där ni bryter ett krav i taget och "
                    + "lämna dem öppna. Då syns det att pipelinen blockerar.",
                    8),
                new("#8", "Aha, då fattar jag. Då gör vi det.", 9),
                new("#15", "Måste de vara öppna, eller räcker det att de är stängda utan merge?", 12),
                new(author, "Öppna, helst. Då syns checkarna direkt när jag klickar in. Stängda går också.", 14),
                new("#15", "Perfekt, tack.", 15)
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
            [
                new("#9", "Vad bra, finns det en chans att få ställa frågor om hur man blir DevOps-ingenjör?", 18),
                new(
                    author,
                    "Absolut. Gästen är DevOps-ingenjör sedan åtta år och har lovat avsätta en halvtimme åt frågor "
                    + "efteråt. Skriv gärna in dem här i förväg så kan jag samla dem.",
                    20),
                new("#3", "Får man fråga om lön?", 21),
                new(author, "Du får fråga. Om du får svar är en annan sak.", 22),
                new("#14", "Hur ofta blir man väckt av jouren egentligen? Det är det jag är mest nyfiken på.", 26)
            ]),
        new(
            "Pipelinen faller på formatering, så här gör ni",
            """
            Flera team har hört av sig om att bygget går rött på `dotnet format --verify-no-changes` eller
            `prettier --check`, trots att det "fungerar lokalt". Det är det som ska hända: pipelinen kontrollerar
            att filerna är formaterade, och den ändrar dem inte åt er.

            Kör `dotnet format` och `npx prettier --write .` lokalt, committa resultatet och pusha igen. Lägg
            gärna in det som en pre-commit-hook, så slipper ni ett rött bygge för ett blanksteg.
            """,
            Se(2026, 10, 27, 13, 20),
            author,
            false,
            true,
            [
                new("#4", "Pipelinen faller på `dotnet format` fast jag formaterar vid spara. Vad är fel?", 1),
                new(
                    "#10",
                    "Hade samma problem! Det var radslut. Min dator använde CRLF och Linux-runnern vill ha LF. "
                    + "Lade till `end_of_line = lf` i `.editorconfig` och `* text=auto eol=lf` i `.gitattributes`, "
                    + "sedan försvann felet.",
                    2),
                new("#4", "Det var precis det. Tusen tack, du räddade min tisdag!", 3),
                new(
                    author,
                    "Bra jobbat, #10. Det är den vanligaste orsaken på Windows. En annan är att "
                    + "formateringsreglerna inte ligger i repot, så att varje dator följer sina egna. "
                    + "Se till att `.editorconfig` är incheckad.",
                    4),
                new("#17", "Går det att låta pipelinen formatera och committa åt oss?", 6),
                new(
                    author,
                    "Det går, men jag rekommenderar det inte här. Då lär ni er aldrig vad som ändrats, och "
                    + "pipelinen skriver till era branchar. Kör `dotnet format` lokalt, det tar tre sekunder.",
                    7)
            ]),
        new(
            "Påminnelse: Docker-laborationen ska in på fredag",
            """
            Laborationen **Docker och containrar** ska in senast **fredag 30 oktober kl. 23.59**. Kom ihåg att
            visa storleken på imagen före och efter, det är den siffran jag letar efter först.

            Fastnar ni på något, kom på handledningen i morgon eftermiddag. Och starta inte arbetet på fredag
            kväll, det blir sällan bättre av det.
            """,
            Se(2026, 10, 29, 7, 45),
            author,
            false,
            true,
            [
                new("#1", "Måste imagen vara under en viss storlek för att bli godkänd?", 0),
                new(
                    author,
                    "Nej, det finns ingen gräns. Men om den är över 500 MB vill jag veta vad som tar plats.",
                    1),
                new("#6", "Funkar ghcr.io om vårt repo är privat?", 1),
                new(author, "Ja, men då behöver ni skapa en token med `write:packages`. Lägg den som secret.", 2),
                new("#19", "Vår app kraschar vid första `compose up` men går bra andra gången. Varför?", 2),
                new(
                    "#6",
                    "Det låter som att databasen inte är redo. Lägg till `healthcheck` på Postgres och "
                    + "`depends_on: condition: service_healthy` på appen. Det löste det för oss.",
                    3)
            ]),
        new(
            "Redovisningar på torsdag 12 november",
            """
            Kursen avslutas med redovisningar **torsdag 12 november**. Varje team visar sitt projekt under 25 minuter,
            följt av 10 minuters frågor.

            | Tid | Team |
            | --- | --- |
            | 09.00 | Team 1 |
            | 09.45 | Team 2 |
            | 10.30 | Fika |
            | 10.50 | Team 3 |
            | 11.35 | Team 4 |
            | 13.00 | Sammanfattning och betyg |

            Visa hur ni går från en ändring till produktion: PR, pipeline, release och rollback. Allt behöver inte
            vara perfekt, men det ska fungera och ni ska kunna förklara varför ni valt som ni gjort. Alla i teamet
            ska prata.
            """,
            Se(2026, 10, 28, 18, 0),
            author,
            true,
            true,
            [
                new("#7", "Måste alla i teamet vara på plats, eller går det att vara med på distans?", 1),
                new(author, "Du får gärna vara med på distans om du är sjuk. Säg till i förväg så ordnar jag länk.", 3),
                new("#11", "Får vi byta tid med ett annat team? Vi har en tandläkartid kl. 10.", 5),
                new(author, "Hör med Team 3 först. Om de går med på det byter vi.", 6),
                new("#11", "De gillar att vi tar 10.50 och att de tar 09.45. Klart!", 8)
            ])
    ];
}
