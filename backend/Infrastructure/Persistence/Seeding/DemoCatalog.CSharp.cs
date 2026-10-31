using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

internal static partial class DemoCatalog
{
    // --- Avslutade kurser som flera klasser läst: samma innehåll, olika datum ---

    private static CourseSpec CSharp(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_CSHARP",
        "Programmering med C#",
        """
        Grunderna i programmering med C# och .NET: typer, kontrollflöden, objektorientering, samlingar, felhantering,
        asynkron kod och enhetstester.

        **Kursmål**

        1. Att skriva, köra och felsöka konsolprogram i C#.
        2. Att använda typer, kontrollflöden och metoder på ett sätt som andra kan läsa.
        3. Att modellera ett problem med klasser, arv och interface.
        4. Att arbeta med samlingar och LINQ.
        5. Att hantera fel med undantag och skriva asynkron kod med async och await.
        6. Att skriva och köra enhetstester med xUnit.
        """,
        30,
        start,
        end,
        author,
        [
            Week(
                "Vecka 1 – Typer, kontrollflöden och metoder",
                "Typer, kontrollflöden, metoder och felhantering.",
                start,
                [
                    Lesson(
                        "Teori: Typer, variabler och kontrollflöden",
                        """
                        Allt i C# har en typ, och kompilatorn kontrollerar att du använder typerna rätt. Det är
                        irriterande de första veckorna och en stor hjälp resten av livet. De typer du använder oftast
                        är `int` för heltal, `double` och `decimal` för decimaltal, `bool` för sant eller falskt,
                        `char` för ett tecken och `string` för text. Använd `decimal` för pengar. `double` är snabb men
                        räknar med binära bråk, så `0.1 + 0.2` blir inte exakt `0.3`, och det vill man inte upptäcka
                        på en faktura.

                        Med `var` låter du kompilatorn räkna ut typen från högersidan. Typen är fortfarande
                        statisk, du sparar bara skrivarbete. Använd `var` när typen syns på samma rad
                        (`var namn = "Ada";`) och skriv ut typen när det annars blir otydligt. Tänk också på skillnaden
                        mellan värdetyper (`int`, `bool`, `struct`) som kopieras vid tilldelning, och referenstyper
                        (`string`, klasser, arrayer) där två variabler kan peka på samma objekt.

                        Programmet styrs med `if`, `else`, `switch`, och loopar: `for` när du vet hur många varv det
                        blir, `foreach` när du går igenom en samling och `while` när du väntar på att något ska
                        inträffa. Ett `switch`-uttryck är ofta tydligare än en kedja av `if`. Fundera också på hur
                        du kan avbryta tidigt med `return`, `break` eller `continue` så att koden inte blir djupt
                        nästlad.

                        ```csharp
                        var summa = 0;
                        foreach (var tal in new[] { 1, 2, 3 })
                        {
                            summa += tal;
                        }

                        string betyg = poäng switch
                        {
                            >= 90 => "A",
                            >= 75 => "B",
                            >= 60 => "C",
                            _ => "F"
                        };
                        ```

                        Läs in text från användaren med `Console.ReadLine()`. Den returnerar `string?`, det vill säga
                        att den kan vara `null` om strömmen är slut. Omvandla med `int.TryParse` i stället för
                        `int.Parse`, så får du ett `false` att hantera i stället för ett krasch när någon skriver
                        "hej" där du ville ha en siffra.

                        ## Typer i praktiken

                        Låt oss säga att du ska räkna ut moms på ett pris som användaren matar in. Då vill du läsa
                        in, kontrollera, räkna och skriva ut, i den ordningen:

                        ```csharp
                        Console.Write("Pris exkl. moms: ");
                        if (!decimal.TryParse(Console.ReadLine(), out var pris) || pris < 0)
                        {
                            Console.WriteLine("Ogiltigt pris.");
                            return;
                        }

                        var moms = pris * 0.25m;
                        Console.WriteLine($"Moms: {moms:F2} kr, totalt {pris + moms:F2} kr");
                        ```

                        Notera `0.25m`. Suffixet `m` gör talet till `decimal`. Utan det får du ett kompileringsfel,
                        och det är kompilatorn som räddar dig från att blanda `double` och `decimal` av misstag.
                        """,
                        "Typer, variabler och kontrollflöden"),
                    Lesson(
                        "Teori: Metoder och felhantering",
                        """
                        En metod gör en sak och har ett namn som säger vilken. Om du behöver ordet "och" för att
                        förklara vad en metod gör, är den troligen två metoder. Metoder tar emot parametrar och kan
                        returnera ett värde. Använd `void` när det inte finns något att returnera. Små metoder är
                        lättare att läsa, lättare att testa och lättare att återanvända, och det är precis det
                        ni ska visa i Tre i rad-labben.

                        Kom ihåg skillnaden mellan att skicka ett argument som värde och som referens. En `int` som
                        skickas in i en metod kopieras, så metoden kan inte ändra din variabel. En lista däremot
                        skickas som en referens, och metoden kan lägga till element i den. Det är en vanlig källa
                        till överraskningar. Med `out` och `ref` kan en metod skicka tillbaka fler värden, men oftast
                        är det bättre att returnera ett litet objekt eller en tuple.

                        När något går fel kastar C# ett **undantag** (exception). Det är inte ett fel i sig utan
                        ett sätt att säga "jag kan inte fortsätta här, någon längre upp får ta hand om det".
                        Du fångar undantag med `try`/`catch`, men bara de du faktiskt kan göra något åt. Ett tomt
                        `catch { }` är det farligaste du kan skriva: felet försvinner och ingen vet varför
                        programmet gör fel.

                        ```csharp
                        static int LäsHeltal(string text)
                        {
                            try
                            {
                                return int.Parse(text);
                            }
                            catch (FormatException)
                            {
                                throw new ArgumentException($"'{text}' är inte ett heltal.", nameof(text));
                            }
                        }
                        ```

                        Fånga den mest specifika typen först (`FormatException`, inte `Exception`), och skriv egna
                        undantagsklasser när anroparen behöver kunna skilja ett fel från ett annat. Använd `finally`
                        eller `using` för att städa upp filer och anslutningar oavsett om det gick bra eller inte.
                        Och validera indata i början av metoden: ett `ArgumentNullException.ThrowIfNull(x)` högst
                        upp sparar dig från ett `NullReferenceException` tio rader längre ned.

                        ## Felhantering i praktiken

                        Tänk dig ett program som läser en fil med poäng, en per rad. Filen kan saknas, en rad kan vara
                        trasig och disken kan vara full. Du vill inte hantera allt på samma ställe. Låt en liten
                        metod läsa en rad och kasta om den är trasig, och låt huvudprogrammet bestämma om det ska
                        hoppa över raden, avbryta eller fråga användaren. Då är det tydligt vem som äger beslutet, och
                        metoden går att testa utan en fil.

                        ```csharp
                        try
                        {
                            var poäng = File.ReadLines(sökväg).Select(LäsHeltal).ToList();
                            Console.WriteLine($"Snitt: {poäng.Average():F1}");
                        }
                        catch (FileNotFoundException)
                        {
                            Console.WriteLine("Filen finns inte. Kontrollera sökvägen.");
                        }
                        ```
                        """,
                        "Metoder och felhantering"),
                    Exercise(
                        "Övningsuppgift: Kata – Ordräknaren",
                        """
                        För att öva på typer, loopar, metoder och felhantering ska vi bygga ett litet
                        terminalprogram som räknar hur många gånger varje ord förekommer i en text. Så här ska
                        det kunna köras:

                        ```text
                        > dotnet run -- "Hej hej och välkommen, hej då"
                        hej: 3
                        och: 1
                        välkommen: 1
                        då: 1

                        > dotnet run -- ""
                        Ange en text att räkna ord i.
                        ```

                        Ordningen på utskriften är först fallande antal, sedan alfabetisk. Versaler och gemener
                        räknas som samma ord, och skiljetecken (`,` `.` `!` `?`) ska inte vara en del av orden.

                        Dela upp programmet i små metoder. Ett förslag: `Normalisera(string text)` returnerar en
                        lista med ord i gemener, `Räkna(IEnumerable<string> ord)` returnerar en `Dictionary<string, int>`
                        och `Skriv(Dictionary<string, int> antal)` sköter utskriften. Du får gärna börja med en
                        `foreach` och skriva om till LINQ (`GroupBy`) när det fungerar. Jämför hur de två
                        versionerna ser ut.

                        Gör så här:

                        - hantera tom text och `null` utan att programmet kraschar
                        - skriv minst tre egna provkörningar (kort text, text med skiljetecken, bara mellanslag)
                        - lägg till flaggan `--top 3` som bara visar de tre vanligaste orden

                        Ingen inlämning, men visa gärna lösningen för en kurskamrat och jämför hur ni delat upp
                        koden. Det är där man lär sig mest.
                        """),
                    Assignment(
                        "Laboration: Tre i rad",
                        """
                        I den här laborationen skriver du ett konsolspel där två spelare turas om att placera sina
                        markörer, `X` och `O`, på ett 3x3-bräde. Du får inget startprojekt, så börja med
                        `dotnet new console` och bygg uppåt därifrån. Spelet ska gå att spela klart från början till
                        slut utan att krascha, hur spelarna än skriver.

                        Programmet ska uppfylla följande:

                        - Brädet ritas ut efter varje drag, med rad- och kolumnnummer så att spelarna vet vad de
                          skriver.
                        - Spelarna anger sitt drag som `rad kolumn`, till exempel `2 3`.
                          - Ogiltiga drag (utanför brädet, redan upptagen ruta, text i stället för siffror) ger ett
                            tydligt meddelande och spelaren får försöka igen.
                          - Ordet `avsluta` avslutar spelet.
                        - Programmet ska känna igen ==vinst och oavgjort== och säga vem som vann.
                        - Efter ett avslutat spel får spelarna välja att spela igen.

                        Koden ska delas upp i metoder som var och en gör en sak. Som riktmärke bör ingen metod
                        vara längre än en skärmbild, och spelets logik (vem har vunnit?) ska vara skild från
                        inläsningen och utskriften. Använd gärna en tvådimensionell array (`char[,]`) för brädet.
                        Ni får använda valfria hjälpklasser men inga färdiga spelbibliotek.

                        💡 Tips

                        Börja med att få ut ett bräde på skärmen och bygg sedan en funktion som kontrollerar vinst,
                        innan du fixar inläsningen. Skriv gärna vinstkontrollen så att den tar brädet som parameter
                        och returnerar `'X'`, `'O'` eller `null`. Då kan du prova den med färdiga brädor utan att spela.

                        Det finns många sätt att kontrollera vinst. Åtta rader av `if` fungerar, men en loop över
                        tre rader, tre kolumner och två diagonaler är kortare och lättare att rätta.

                        ## Att lämna in

                        - [ ] Länk till repot (eller en zip) med hela projektet
                        - [ ] En kort text som beskriver hur du delat upp koden i metoder och varför
                        - [ ] Ett exempel på en spelomgång (klistra in terminalutskriften)

                        **Betygskriterier**

                        Du blir godkänd om alla punkter ovan fungerar och koden är uppdelad i begripliga metoder.
                        Laborationen examinerar kursmål 1, "Att skriva, köra och felsöka konsolprogram i C#", och
                        kursmål 2, "Att använda typer, kontrollflöden och metoder på ett sätt som andra kan läsa".
                        """,
                        Friday(start, 2),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Jag har delat upp spelet i `RitaBräde`, `LäsDrag`, `ÄrLedigt`, `HittaVinnare` och "
                            + "`Main`. Vinstkontrollen går igenom tre rader, tre kolumner och två diagonaler i en "
                            + "loop. Ogiltiga drag ger ett meddelande och spelaren får försöka igen. En omgång finns i "
                            + "README.",
                            "Brädet är en `char[,]`. Jag lärde mig den hårda vägen att `int.Parse` kraschar när man "
                            + "skriver ett mellanslag, så jag bytte till `TryParse`. Oavgjort känner jag igen när "
                            + "alla nio rutor är fyllda utan vinnare.",
                            "Spelet fungerar och man kan spela igen efteråt. Jag ångrar att jag skrev all logik i "
                            + "`Main` från början, så jag bröt ut den i sju metoder i efterhand. Det blev mycket "
                            + "lättare att hitta felet i diagonalerna.",
                            "Vi var två som samarbetade om idéerna men skrev varsin lösning. Min har en egen klass "
                            + "`Bräde` med metoden `Placera(rad, kolumn, spelare)` som returnerar false om rutan är "
                            + "upptagen."
                        ],
                        [
                            "Bra uppdelning. `HittaVinnare` går att testa utan att spela, precis som vi pratade om. "
                            + "Godkänd.",
                            "Godkänd. Hanteringen av ogiltiga drag är riktigt bra, men `avsluta` fungerar bara när "
                            + "det skrivs med små bokstäver. Kolla det nästa gång.",
                            "Tydlig redovisning av metoderna och fina provkörningar. Godkänd."
                        ],
                        [
                            "Nästan där: spelet kraschar om man skriver text där ett tal förväntas. Byt `int.Parse` "
                            + "mot `int.TryParse`, skriv ut ett meddelande och lämna in igen.",
                            "Jag ser ingen kontroll för oavgjort, så spelet fortsätter när brädet är fullt. "
                            + "Komplettera och beskriv hur du löst det.",
                            "All logik ligger i `Main`. Dela upp i metoder enligt kraven och lämna in på nytt."
                        ],
                        was: "Konsolspel: Tre i rad")
                ],
                "Grunderna i C#"),
            Week(
                "Vecka 4 – Objektorientering, samlingar och LINQ",
                "Klasser, arv, interface, samlingar, LINQ och asynkron kod.",
                start.AddDays(21),
                [
                    Lesson(
                        "Teori: Klasser, arv och interface",
                        """
                        En klass samlar data och det beteende som hör ihop med datan. En `Medlem` har ett namn och en
                        lista med lån, och metoden `LånaBok` ligger på medlemmen eller på ett lånesystem, inte i
                        `Main`. Poängen är att dölja detaljer: ingen utanför klassen ska kunna sätta en medlems
                        lånelista till `null`. Därför gör man fält privata, exponerar egenskaper med
                        `{ get; private set; }` och låter konstruktorn se till att objektet alltid är giltigt.

                        Arv betyder "är en sorts". En `Student` är en `Person`, så den kan återanvända `Namn` och
                        `Epost`. Men arv binder ihop klasserna hårt, och en ändring i basklassen slår mot alla
                        subklasser. Därför gäller tumregeln: föredra komposition framför arv. Låt en `Bok` ha ett
                        `Isbn` i stället för att ärva från `Isbn`. Arv passar när det finns ett äkta
                        "är en"-förhållande och du vill ha polymorfism, det vill säga att samma anrop ger olika
                        beteende beroende på objektets verkliga typ.

                        Ett **interface** beskriver vad något kan göra utan att säga hur. Det är kontraktet mellan
                        delar av programmet, och det gör att du kan byta ut en implementation mot en annan, till
                        exempel en riktig databas mot en lista i minnet i ett test. Ett interface kan implementeras av
                        många klasser och en klass kan implementera många interface, vilket arv inte tillåter.

                        ```csharp
                        public interface IBokregister
                        {
                            IReadOnlyList<Bok> Sök(string text);
                            void Lägg(Bok bok);
                        }

                        public class MinnesRegister : IBokregister
                        {
                            private readonly List<Bok> _böcker = [];

                            public IReadOnlyList<Bok> Sök(string text) =>
                                _böcker.Where(b => b.Titel.Contains(text, StringComparison.OrdinalIgnoreCase))
                                       .ToList();

                            public void Lägg(Bok bok) => _böcker.Add(bok);
                        }

                        public record Bok(string Isbn, string Titel, string Författare);
                        ```

                        Notera `record` på sista raden. Ett record är en klass för data som inte ska ändras: du får
                        jämförelse på innehåll, `ToString` och `with`-uttryck gratis. Perfekt för en bok.

                        ## Klasser i praktiken

                        Tänk dig biblioteksystemet. En `Medlem` får låna högst fem böcker. Var ska den regeln ligga?
                        Inte i användargränssnittet, för då gäller den bara där. Lägg den i `Medlem.Låna(Bok bok)`
                        och kasta ett eget undantag, `FörMångaLånException`, när gränsen nås. Då gäller regeln
                        oavsett om det är en konsol, en webbsida eller ett test som anropar.
                        """,
                        "Klasser, arv och interface"),
                    Lesson(
                        "Teori: Samlingar och LINQ",
                        """
                        De samlingar du kommer använda mest är `List<T>` (en växande lista), `Dictionary<TKey, TValue>`
                        (uppslag på nyckel) och `HashSet<T>` (unika värden, snabbt att fråga om något finns).
                        Välj efter hur du ska använda datan. Behöver du hitta en medlem på medlemsnummer är en
                        `Dictionary` snabbare än att leta igenom en lista, och den berättar dessutom för läsaren hur
                        koden tänkt.

                        **LINQ** (Language Integrated Query) låter dig fråga samlingar med samma byggstenar överallt.
                        `Where` filtrerar, `Select` omvandlar, `OrderBy` sorterar, `GroupBy` grupperar och `Any`,
                        `Count`, `Sum` och `FirstOrDefault` svarar på frågor. Frågorna är **lata**: de körs inte
                        förrän du går igenom resultatet eller anropar något som `ToList()`. Det spelar roll när du
                        ändrar samlingen emellan, och det är anledningen till att man ibland får samma fråga körd
                        två gånger av misstag.

                        ```csharp
                        var försenade = lån
                            .Where(l => l.Återlämnad is null && l.Förfaller < DateTime.Today)
                            .OrderBy(l => l.Förfaller)
                            .Select(l => new { l.Medlem.Namn, l.Bok.Titel, Dagar = (DateTime.Today - l.Förfaller).Days })
                            .ToList();

                        var perFörfattare = böcker
                            .GroupBy(b => b.Författare)
                            .Select(g => new { Författare = g.Key, Antal = g.Count() })
                            .OrderByDescending(x => x.Antal);
                        ```

                        Vill du skriva det på ett annat sätt finns frågesyntaxen (`from b in böcker where ... select b`),
                        men metodsyntaxen ovan är den du ser mest i riktig kod. Se upp med `First()` och
                        `Single()`: de kastar ett undantag om inget finns eller om det finns flera. Använd
                        `FirstOrDefault()` och kontrollera `null` om "ingen träff" är ett normalt läge.

                        ## LINQ i praktiken

                        Ett vanligt misstag är att bygga en `foreach` med en inre `if` och en `List` som man fyller på.
                        Det går att göra, men skriv om det med `Where` och `Select` och jämför. Oftast blir koden
                        hälften så lång och beskriver *vad* du vill ha i stället för *hur* du tar dig dit. Kom ändå ihåg att
                        läsbarheten bestämmer: tre kedjade `GroupBy` med anonyma typer är inte alltid ett bättre svar.
                        """,
                        "Samlingar och LINQ"),
                    Lesson(
                        "Teori: Asynkron programmering med async och await",
                        """
                        När programmet väntar på något utanför sig självt, som en fil, ett nätverksanrop eller en
                        databas, vill vi inte låsa tråden under tiden. Med **async** och **await** kan metoden lämna
                        tillbaka kontrollen medan den väntar, och fortsätta där den slutade när svaret kommer. Det är
                        inte parallellism, det är att inte stå och vänta i onödan. I en webbtjänst betyder det att
                        samma tråd kan betjäna många fler användare.

                        Du känner igen asynkrona metoder på att de returnerar `Task` eller `Task<T>` och heter något på
                        `Async`. Anropar du dem med `await` får du värdet när det är klart. Glömmer du `await` får
                        du en `Task` i stället för resultatet, och kompilatorn varnar. Undvik `.Result` och
                        `.Wait()`, som blockerar tråden och kan låsa hela programmet.

                        ```csharp
                        static async Task<int> HämtaLängdAsync(string url)
                        {
                            using var klient = new HttpClient();
                            var text = await klient.GetStringAsync(url);
                            return text.Length;
                        }

                        var längder = await Task.WhenAll(
                            HämtaLängdAsync("https://learn.microsoft.com"),
                            HämtaLängdAsync("https://dotnet.microsoft.com"));
                        Console.WriteLine(längder.Sum());
                        ```

                        Undantag i asynkrona metoder fångas med vanlig `try`/`catch` runt `await`. Med
                        `CancellationToken` kan anroparen avbryta ett pågående arbete på ett snyggt sätt, och det är
                        något ni kommer se överallt i ASP.NET Core. Skriv inte `async void` utom för
                        händelsehanterare: du får inget sätt att fånga felen.

                        ## Async i praktiken

                        Nästan alla frågor mot en databas i den här utbildningen är asynkrona. Gör `async` hela
                        vägen: om en metod längst ned är asynkron ska metoderna som anropar den också vara det.
                        Blandar du synkront och asynkront får du de konstigaste buggarna.
                        """),
                    Exercise(
                        "Övningsuppgift: Kata – Lånerapporten",
                        """
                        För att öva på samlingar och LINQ ska vi utgå från den här listan med lån, som du får
                        lägga in direkt i koden:

                        ```csharp
                        record Lån(string Medlem, string Titel, DateTime Förfaller, DateTime? Återlämnad);
                        ```

                        Skapa ett konsolprogram som skriver ut en rapport. En körning kan se ut så här:

                        ```text
                        > dotnet run
                        Försenade lån (3):
                          Ada Lovelace    Clean Code              12 dagar
                          Alan Turing     Domain-Driven Design     5 dagar
                          Ada Lovelace    The Pragmatic Programmer 2 dagar

                        Lån per medlem:
                          Ada Lovelace    4
                          Alan Turing     2
                          Grace Hopper    1
                        ```

                        Lös uppgiften med LINQ, utan att skriva en enda `for`-loop. Börja med `Where` och
                        `OrderByDescending` för de försenade lånen och använd sedan `GroupBy` för statistiken. Prova
                        också att skriva samma sak med frågesyntaxen och se vilken du tycker är lättast att läsa.

                        Skriv extra rapporter när du är klar:

                        - medlemmar som aldrig lämnat tillbaka något i tid
                        - vilken titel som lånats flest gånger
                        - hur många lån som är aktiva just nu

                        Kontrollera att rapporten fungerar även när listan är tom. Då ska programmet skriva "Inga lån
                        att visa." i stället för att krascha.
                        """),
                    Assignment(
                        "Laboration: Biblioteksystem med OOP",
                        """
                        I den här laborationen bygger du ett litet biblioteksystem för böcker, medlemmar och lån.
                        Det är en konsolapplikation, men designen ska vara sådan att man hade kunnat byta ut
                        konsolen mot en webbsida utan att röra affärsreglerna. Du bestämmer själv hur ditt program
                        ser ut inuti, men följande krav gäller:

                        - Minst tre klasser (eller records): `Bok`, `Medlem` och `Lån`.
                        - Minst ett ==interface== som beskriver hur böcker lagras och söks, till exempel
                          `IBokregister`, och minst en implementation som lagrar i minnet.
                        - Affärsregler som ligger i domänklasserna, inte i `Main`:
                          - en medlem får låna högst fem böcker samtidigt
                          - en bok kan bara vara utlånad till en medlem åt gången
                          - ett lån förfaller efter 28 dagar
                          - försök att bryta en regel kastar ett eget undantag med ett tydligt meddelande
                        - Sökning med LINQ: på titel, på författare och en lista över försenade lån.
                        - En asynkron metod, till exempel `SparaAsync(...)` som skriver registret till en JSON-fil
                          med `System.Text.Json`.

                        Ni får använda valfria hjälpbibliotek, men inget ORM, eftersom ni inte läst databaser än.
                        Skriv ner dina designval och motivera dem: varför blev något en klass och inte ett
                        interface, och vad hade du gjort annorlunda med mer tid?

                        💡 Tips

                        Börja med modellen och skriv en enkel `Main` som bara visar att reglerna fungerar innan du
                        bygger en meny. Det är lättare att fixa ett fel i domänen när du inte också har ett
                        gränssnitt att felsöka.

                        Du bestämmer själv hur sökningen ska fungera (innehåller texten eller börjar med den, versaler
                        eller inte), men var konsekvent och beskriv valet i redovisningen.

                        ## Att lämna in

                        - [ ] Länk till repot med projektet
                        - [ ] Beskrivning av klasserna och hur de hänger ihop (en enkel skiss räcker)
                        - [ ] Motivering av dina designval, minst ett stycke
                        - [ ] Ett exempel som visar att en regel bryts och att undantaget fångas

                        **Betygskriterier**

                        För Godkänt ska alla krav ovan vara uppfyllda. För Väl godkänt ska dessutom designen vara
                        genomtänkt och motiverad: du ska kunna förklara varför du valde komposition eller arv på
                        varje ställe, ha egna undantagstyper, och använda `async` och `CancellationToken` på rätt
                        sätt. Laborationen examinerar kursmål 3 (klasser, arv och interface), kursmål 4 (samlingar och
                        LINQ) och kursmål 5 (undantag och asynkron kod).
                        """,
                        Friday(start, 5),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Jag har `Bok`, `Medlem` och `Lån` och interfacet `IBokregister` med `MinnesRegister`. "
                            + "Femboksgränsen ligger i `Medlem.Låna` och kastar `FörMångaLånException`. Sökningen är "
                            + "LINQ med `Contains` och `StringComparison.OrdinalIgnoreCase`. Jag valde composition: "
                            + "ett `Lån` har en `Bok` och en `Medlem`, inget ärver.",
                            "Registret sparas till JSON med en `SparaAsync`. Jag provade att ta bort `await` för att "
                            + "se vad som händer, och filen blev tom ibland. Försenade lån tar jag fram med "
                            + "`Where` och `OrderBy`. Jag hade gärna haft en riktig databas, men den kommer väl "
                            + "i nästa kurs.",
                            "Jag försökte ärva `Student` och `Lärare` från `Medlem` men det gav ingenting, så jag "
                            + "tog bort arvet och lät medlemmen ha en `Roll` i stället. Det blev enklare.",
                            "Bokregistret är ett interface så att jag kan använda en lista i minnet nu och en fil sedan. "
                            + "Reglerna är testade för hand i `Main`. Jag sparar en `Dictionary` med medlemsnummer som "
                            + "nyckel eftersom jag söker på det hela tiden.",
                            "Vi är nöjda med domänen men menyn i `Program.cs` blev för lång. Vi vet att den borde "
                            + "brytas ut och skriver det som en reflektion."
                        ],
                        [
                            "Mycket bra: `IBokregister` används på riktigt, reglerna ligger i domänen och "
                            + "undantagen har bra meddelanden. Godkänd.",
                            "Tydlig motivering av komposition framför arv och en bra `SparaAsync` med "
                            + "`CancellationToken`. Det lyfter till VG.",
                            "Godkänd. LINQ-frågorna är läsbara. Du får gärna fundera på om `GroupBy` hade gjort "
                            + "listan över försenade lån ännu kortare."
                        ],
                        [
                            "Femboksgränsen kontrolleras i `Main` och inte i `Medlem`. Flytta regeln till domänen och "
                            + "lämna in igen.",
                            "Det saknas ett interface för registret, så kravet är inte uppfyllt än. Bryt ut "
                            + "`IBokregister`, och beskriv varför det hjälper.",
                            "Redovisningen av designvalen är för kort. Jag behöver förstå varför du valde som du gjorde, "
                            + "inte bara vad som finns."
                        ],
                        was: "Biblioteksystem med OOP")
                ],
                "Objektorienterad programmering"),
            Week(
                "Vecka 7 – Testning och kodkvalitet",
                "Enhetstester med xUnit.",
                start.AddDays(42),
                [
                    Lesson(
                        "Teori: Enhetstester med xUnit",
                        """
                        Ett enhetstest är ett litet program som kör en liten del av din kod och kontrollerar att
                        resultatet blir det du väntat dig. Det gör att du vågar ändra koden: går något sönder
                        ser du det direkt, i stället för att en användare gör det en månad senare. Tester är också
                        en sorts dokumentation, för de visar hur koden är tänkt att användas.

                        Ett test följer mönstret **Arrange, Act, Assert**. Du förbereder (skapar objekten), du gör
                        det som ska testas (anropar metoden) och du kontrollerar resultatet. Håll det till en
                        sak per test, och döp testet så att namnet förklarar felet när det går sönder, till exempel
                        `Låna_SjundeBoken_KastarFörMångaLån`.

                        ```csharp
                        public class MedlemTests
                        {
                            [Fact]
                            public void Låna_FemBöcker_Går()
                            {
                                var medlem = new Medlem("Ada");
                                for (var i = 0; i < 5; i++)
                                {
                                    medlem.Låna(new Bok($"isbn{i}", "Titel", "Författare"));
                                }

                                Assert.Equal(5, medlem.Lån.Count);
                            }

                            [Fact]
                            public void Låna_SjätteBoken_Kastar()
                            {
                                var medlem = new Medlem("Ada");
                                for (var i = 0; i < 5; i++)
                                {
                                    medlem.Låna(new Bok($"isbn{i}", "Titel", "Författare"));
                                }

                                Assert.Throws<FörMångaLånException>(
                                    () => medlem.Låna(new Bok("isbn6", "Titel", "Författare")));
                            }

                            [Theory]
                            [InlineData("")]
                            [InlineData("   ")]
                            public void Ny_TomtNamn_Kastar(string namn) =>
                                Assert.Throws<ArgumentException>(() => new Medlem(namn));
                        }
                        ```

                        `[Fact]` är ett test utan parametrar, och `[Theory]` med `[InlineData]` kör samma test med
                        flera värden. Kör testerna med `dotnet test`. Skriv tester för felfall, gränsvärden och
                        tomma värden, inte bara för det som borde fungera.

                        ## Tester i praktiken

                        Vi har inte som mål att nå 100 procents täckning. Täckning (hur stor del av koden som körs av
                        testerna) säger hur mycket du *inte* testat, inte att resten är rätt. Använd den som en
                        kompass: leta upp de delar av domänen med affärsregler som saknar tester och börja där. Och
                        ett test som kräver att du skriver om hälften av koden för att det ska gå att köra är
                        information om designen. Interface och beroenden som skickas in i konstruktorn är det som
                        gör koden testbar.
                        """,
                        "Enhetstester med xUnit"),
                    Assignment(
                        "Laboration: Tester för biblioteksystemet",
                        """
                        I den här laborationen skriver du tester för biblioteksystemet från förra laborationen.
                        Du kan fortsätta med ditt eget projekt eller välja ett kurskamratens, om du bytt kod med
                        någon. Lägg testerna i ett eget xUnit-projekt (`dotnet new xunit`) som refererar till
                        biblioteksprojektet.

                        Testerna ska uppfylla följande:

                        - Minst ==åtta enhetstester==, där minst tre testar felfall (regler som bryts, ogiltiga
                          argument, tomma värden).
                        - Testerna följer Arrange, Act, Assert och har namn som beskriver vad som testas.
                        - Minst ett `[Theory]` med `[InlineData]`.
                        - Testerna ska vara oberoende av varandra och kunna köras i valfri ordning.
                        - Minst ett test som använder en minnesimplementation av `IBokregister` i stället för filen.

                        Testa affärsreglerna: lånegränsen, att en utlånad bok inte kan lånas igen och att
                        försenade lån identifieras. Testa dem med olika värden, gärna gränsvärden som fem och sex
                        böcker, och med tomma eller ogiltiga värden.

                        Redovisa testtäckningen med `dotnet test --collect:"XPlat Code Coverage"` och förklara
                        vilka delar som inte täcks och varför.

                        💡 Tips

                        Hittar du ett fel i din egen kod medan du testar? Bra, det är därför man skriver tester. Rätta
                        felet och skriv ett test som hade fångat det.

                        Det är ingen idé att försöka täcka `Main`. Flytta hellre logik ut ur den till klasser som går
                        att testa.

                        ## Att lämna in

                        - [ ] Länk till repot med test- och biblioteksprojekt
                        - [ ] Utskrift av `dotnet test` som visar att alla tester går igenom
                        - [ ] Redovisning av testtäckningen med en kommentar om vad som saknas

                        **Betygskriterier**

                        För Godkänt ska alla krav ovan vara uppfyllda. För Väl godkänt krävs dessutom att testerna
                        är väl valda: de ska fånga verkliga fel (visa gärna ett test som failar när du medvetet
                        förstör koden), vara snabba och enkla att läsa. Laborationen examinerar kursmål 6, "Att
                        skriva och köra enhetstester med xUnit", samt kursmål 5 för felfallen.
                        """,
                        Friday(start, 8),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Jag har elva tester i `BiblioteksTests`. Tre av dem kontrollerar felfall, bland annat "
                            + "`Låna_SjätteBoken_Kastar` och `Ny_TomtNamn_Kastar` som är en `Theory` med tre värden. "
                            + "Täckningen blev 82 procent. Det som saknas är utskrifterna i `Program.cs`.",
                            "Åtta tester som följer Arrange, Act, Assert. Jag provade att ta bort en kontroll i `Låna` "
                            + "och då blev två tester röda, så jag litar på dem. Registret byttes ut mot "
                            + "`MinnesRegister` i alla tester.",
                            "Mina tester är nio till antalet. Jag fick skriva om `Medlem` så att den tog emot "
                            + "ett register i konstruktorn, det var det som gjorde den testbar. Täckning 74 procent.",
                            "Jag har tio tester och en `Theory` för gränsvärdena fyra, fem och sex böcker. Den "
                            + "som tog längst var att testa de försenade lånen, för jag behövde skicka in datumet i "
                            + "stället för att använda `DateTime.Today`."
                        ],
                        [
                            "Bra testnamn och en tydlig `Theory` för gränsvärdena. Godkänd.",
                            "Mycket bra: du visar att testerna fångar ett medvetet fel och resonerar om täckningen "
                            + "på rätt sätt. Det lyfter till VG.",
                            "Godkänd. Testerna är oberoende och snabba. Skicka in tiden som en parameter nästa gång "
                            + "så slipper du bero på dagens datum."
                        ],
                        [
                            "Du har sju tester, och kravet är minst åtta. Lägg till ett test för felfall och "
                            + "lämna in igen.",
                            "Två av testen beror på varandra eftersom de delar samma `Medlem`. Skapa ett nytt objekt "
                            + "i varje test så att de kan köras i valfri ordning.",
                            "Det finns inget `Theory`-test och redovisningen av täckningen saknas. Komplettera båda."
                        ],
                        was: "Tester för biblioteksystemet")
                ],
                "Testning och kodkvalitet")
        ],
        CSharpNews(start, author));

    private static NewsSpec[] CSharpNews(DateOnly start, string author) =>
    [
        new(
            "Välkommen till Programmering med C#",
            """
            Välkommen till kursen **Programmering med C#**. Vi börjar med grunderna och bygger upp via
            objektorientering och LINQ till enhetstester. Kursen har tre laborationer och en tenta, och
            allt material hittar ni under veckorna här i kursrummet.

            Installera .NET SDK och en editor (Visual Studio, Rider eller VS Code med C# Dev Kit) före första
            lektionen. Kör `dotnet --version` i terminalen för att kontrollera att det fungerar.
            """,
            Se(start.AddDays(-3), 14),
            author,
            true,
            true,
            [
                new("#1", "Går det bra att använda Rider i stället för Visual Studio?", 3),
                new("#4", "Jag kör Rider själv och det fungerar fint. Du behöver bara .NET SDK installerat.", 4),
                new(author, "Precis, båda går bra. Välj det ni trivs med, jag visar saker i Visual Studio men det "
                    + "mesta är detsamma.", 6),
                new("#8", "Tack!", 7)
            ]),
        new(
            "Tips: material att öva med mellan lektionerna",
            """
            Fler än ett par av er har frågat var man kan öva mer. Här är mina tre favoriter:

            - **Microsoft Learn** har en gratis och bra introduktion till C# med övningar i webbläsaren.
            - **Exercism** (C#-spåret) ger små uppgifter med tester och feedback från mentorer.
            - **Advent of Code** pågår i december, men gamla år går bra att köra när som helst.

            Tanken är inte att ni ska göra allt, utan att ni hittar något som är roligt att göra en halvtimme i
            veckan. Det ger mer än en lång kväll innan en deadline.
            """,
            Se(start.AddDays(14), 15),
            author,
            false,
            true,
            [
                new("#6", "Exercism har jag provat, det är grymt att man får tester på en gång.", 5),
                new("#12", "Finns det något liknande för LINQ specifikt?", 9),
                new("#6", "Det finns en LINQ-övning i C#-spåret på Exercism. Annars kan du hitta på egna "
                    + "frågor mot listan i Lånerapporten-katan.", 11),
                new(author, "Bra svar. Ta gärna med era favoriter om ni hittar fler, så lägger jag upp en lista.", 14)
            ]),
        new(
            "Extra hjälpträff inför biblioteksystemet",
            """
            Det har kommit många frågor om **Biblioteksystem med OOP**, så jag lägger in en extra hjälpträff i
            veckan, onsdag kl. 15.00 i sal B204. Ta med din dator och det du har hunnit, så sitter vi och tittar
            på designen tillsammans. Det går bra att komma även om ni inte kört fast, det är ofta då man har bäst
            frågor.

            Laborationen ska in på fredag kl. 23.59.
            """,
            Se(start.AddDays(28), 10),
            author,
            false,
            true,
            [
                new("#3", "Kan man komma bara en kvart? Jag har bara en fråga om interface.", 2),
                new(author, "Absolut, kom när det passar. Jag är där hela tiden.", 3),
                new("#10", "Spelas det in?", 5),
                new(author, "Nej, det är en handledning och inte en föreläsning. Men skriv gärna era frågor i "
                    + "kommentarerna här så svarar jag där också.", 6),
                new("#10", "Ok, då kommer jag!", 7)
            ]),
        new(
            "Tentan: ny sal",
            """
            En ändring inför tentan: den skrivs **torsdag kl. 08.30**, men i **sal C112** i stället för B204,
            eftersom fler anmält sig än vi först trodde. Kom i god tid och ta med ID-handling. Tentan skrivs
            på papper, och ni får ha en handskriven A4 (båda sidor) som hjälpmedel.

            Tentan täcker hela kursen, men tyngdpunkten ligger på klasser, LINQ och undantag. Gamla tentor hittar ni
            i materialet.
            """,
            Se(start.AddDays(49), 9),
            author,
            true,
            true,
            [
                new("#2", "Får man ha en vanlig miniräknare?", 1),
                new(author, "Ja, men du behöver den troligen inte. Det är kod och förklaringar, inga beräkningar.", 3),
                new("#14", "Kan man få skriva på dator? Jag har dyslexi.", 4),
                new(author, "Hör av dig till mig privat så ordnar vi anpassad tentamen med dator i en egen sal.", 5),
                new("#14", "Tack, mejlar nu.", 5)
            ]),
        new(
            "Så gick tentan",
            """
            Tack för i torsdags, nu har jag rättat alla tentor. De flesta klarade sig bra: särskilt klasserna och
            LINQ satt i ryggmärgen. Det som oftast gick snett var **undantag** (många fångar `Exception` när de
            borde fånga något mer specifikt) och `async`, där flera glömde `await`.

            Resultaten finns i kursrummet och ni som vill se er tenta kan kolla den med mig i veckan. Är ni
            missnöjda finns en omtenta, datumet kommer under veckan.
            """,
            Se(start.AddDays(58), 14),
            author,
            false,
            true,
            [
                new("#0", "Tack! Var det meningen att fråga fyra skulle ha två rätta svar?", 3),
                new(author, "Nej, det var en miss på alternativ C. Jag har rättat så att båda gav poäng.", 5),
                new("#5", "Skönt att det är över. Nu kommer databaser!", 6),
                new(author, "Precis. Vila lite, sedan börjar vi med SQL.", 8)
            ])
    ];
}
