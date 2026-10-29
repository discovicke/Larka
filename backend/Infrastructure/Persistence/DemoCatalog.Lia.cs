using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.DemoPeople;

namespace Backend.Infrastructure.Persistence;

internal static partial class DemoCatalog
{
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
                            "Teori: Så hittar du en LIA-plats",
                            """
                            Att hitta en LIA-plats är ett jobb i sig, och det är en bra övning inför att söka
                            anställning sedan. De flesta platser hittas inte genom annonser. De hittas genom att
                            någon känner någon, att man hälsat på ett företag på ett branschevenemang eller helt
                            enkelt att man frågat. Så börja med det du redan har: familj, vänner, tidigare
                            arbetsgivare, kurskamrater som redan varit ute och de som tidigare gått utbildningen.

                            Gör sedan en lista på tio till femton företag. Blanda stora och små. Ett litet
                            företag på 15 personer ger ofta mer ansvar och en handledare som sitter bredvid, medan en
                            stor organisation har bättre rutiner och fler att lära av. Båda kan vara bra, så det
                            beror på vad du själv vill ha. Kommuner, regioner och myndigheter har ofta många
                            utvecklare och tar emot praktikanter varje år. Leta även efter konsultbolag, för de
                            har många olika kunder att lära av.

                            Skriv ner tre företag som du verkligen vill till och kontakta dem personligen innan du
                            skickar ut breda ansökningar. Ring eller skriv direkt till en utvecklingschef eller
                            teamledare, för en rekryterare på HR vet sällan vad en LIA-student kan göra. Varje kontakt
                            som inte leder till plats ger ändå något: ett namn, ett tips eller en fråga du kan
                            förbereda dig på till nästa gång.

                            ## Att tänka på när du väljer

                            Plats på ett företag är godkänd först när utbildningsledaren har godkänt den. Kontrollera
                            därför
                            tre saker innan du tackar ja:

                            - att du får arbeta med utveckling, inte bara support eller testning
                            - att du får en utsedd handledare som har tid att träffa dig varje vecka
                            - att företaget kan ta emot dig under hela perioden, 18 veckor på heltid

                            Känner du dig osäker på om en plats räcker, så fråga oss innan du svarar. Det är bättre
                            att reda ut det nu än under vecka tre.
                            """,
                            "Så hittar du en LIA-plats"),
                        Lesson(
                            "Teori: Personligt brev och CV",
                            """
                            Ett CV för LIA ska vara på en sida. Börja med en rad om vem du är ("Systemutvecklare
                            under utbildning med inriktning på .NET och AI"), följt av teknik du använt, projekt du
                            byggt och tidigare arbeten. Ett jobb på ICA eller i vården visar ansvar och att du kan
                            samarbeta, så ta med det även om det inte är IT.

                            Det personliga brevet ska vara kort och konkret, fyra till sex meningar räcker. Berätta
                            vad du kan, vad du vill lära dig och varför just det här företaget. "Jag har byggt ett
                            bokningssystem med ASP.NET Core och Vue och vill lära mig hur ni arbetar med
                            driftsättning" säger mer än tre rader om att du är noggrann och driven. Länka till ett
                            projekt på GitHub som du är stolt över, och se till att README:n förklarar vad det är.

                            ## Första mejlet

                            Mejlet du skickar första gången avgör om någon läser resten. Använd gärna mallen här
                            och skriv om den så att den låter som du:

                            ```text
                            Ämne: LIA-plats som systemutvecklare, 18 januari till 28 maj

                            Hej Anna,

                            Jag heter [Namn] och läser till systemutvecklare .NET på [skolan]. Jag söker en LIA-plats
                            från 18 januari till 28 maj och har sett att ni arbetar med [något konkret, till exempel
                            ert bokningssystem eller att ni använder Azure].

                            Jag har byggt [projekt, med länk] och vill gärna lära mig [något ni gör]. Får jag ringa
                            eller komma förbi i tio minuter för att höra om det finns plats hos er?

                            CV och länk till mina projekt finns bifogade.

                            Vänliga hälsningar
                            [Namn], [telefon]
                            ```

                            Följ upp efter en vecka om du inte hört något. Det handlar inte om att tjata, mejl
                            hamnar ofta längst ned i någons inkorg.
                            """,
                            "Personligt brev och CV"),
                        Lesson(
                            "Teori: Vad handledaren förväntar sig",
                            """
                            Handledaren är en vanlig utvecklare som fått uppdraget att ta hand om dig, utöver sitt
                            ordinarie arbete. Det betyder att tiden är begränsad, och att det hon eller han förväntar
                            sig av dig mest handlar om hur du är som kollega, inte hur mycket du redan kan.

                            Det som uppskattas mest är att du är nyfiken och tar initiativ. Fråga när du fastnat, men
                            försök först ett par kvartar på egen hand och berätta vad du provat när du frågar. Det
                            visar att du tänker, och handledaren kan svara snabbare. Be om feedback på din kod, och
                            gör något åt den du får. En student som får samma kommentar tre gånger i rad märks.

                            Var med i teamets rutiner: standup, planering, kodgranskning och retrospektiv. Kom i tid,
                            säg
                            ifrån om du blir sjuk innan arbetsdagen börjar och håll avtalad arbetstid. Du kommer
                            att få ta del av kod och information som inte är offentlig, och då gäller
                            tystnadsplikt även när du är klar.

                            ## Det som oftast går snett

                            - du sitter fast i flera timmar utan att säga något
                            - du får en uppgift som är för stor och säger inte det
                            - du väntar på att få uppgifter i stället för att fråga vad som kan göras
                            - förväntningarna har aldrig sagts högt, så ingen vet vad "bra" är

                            Det sista är lätt att åtgärda. Be om ett samtal första veckan och fråga: "Vad skulle få
                            dig att
                            tycka att det här gått bra om tre månader?"
                            """),
                        Exercise(
                            "Övningsuppgift: Skriv ditt första mejl",
                            """
                            Skriv ett mejl till ett företag du kan tänka dig att göra LIA hos, med hjälp av mallen i
                            teorin. Du behöver inte skicka det, men skriv det som om du skulle göra det.

                            Kontrollera att mejlet:

                            - nämner något konkret om just det företaget
                            - säger vad du har gjort och vill lära dig
                            - har en tydlig fråga som är lätt att svara ja eller nej på
                            - ryms på en skärm utan att man behöver skrolla

                            Klistra gärna in det i svaret om du vill ha synpunkter. Jag eller en kurskamrat tittar
                            på det.
                            """,
                            _text),
                        Assignment(
                            "Inlämning: Vald LIA-plats",
                            """
                            När du har fått ett ja ska platsen godkännas av utbildningsledaren innan den är klar.
                            Skicka in följande uppgifter här:

                            - företagets namn och ort, och om du arbetar på plats, hemifrån eller blandat
                            - handledarens namn, roll, e-post och telefonnummer
                            - en kort beskrivning av vad du kommer att arbeta med och vilken teknik som används
                            - det du förväntar dig att lära dig under perioden

                            Vi kontaktar handledaren för att gå igenom upplägget och skickar ett avtal som ni skriver
                            under.
                            Lämna in ==senast 20 november==. Har du inte hittat plats då, hör av dig så tidigt som
                            möjligt. Vi har kontakter och hjälper dig, men det går inte att starta LIA utan plats.

                            ## Att lämna in

                            - [ ] Företag, handledare och kontaktuppgifter
                            - [ ] Vad du ska arbeta med och med vilken teknik
                            - [ ] Dina egna mål med perioden

                            **Bedömning**

                            Uppgiften bedöms som godkänd eller icke godkänd. Godkänd betyder att platsen uppfyller
                            kraven i teorin: utveckling som arbetsuppgift, en utsedd handledare och hela perioden.
                            """,
                            Se(2026, 11, 20, 23, 59),
                            GradeScale.PassFail,
                            _text,
                            SubmissionPattern.None,
                            was: "Skicka in vald LIA-plats")
                    ]),
                new(
                    "Under LIA",
                    "Veckologg och uppföljning.",
                    Se(start, 7),
                    0,
                    [
                        Lesson(
                            "Praktik: Din roll under LIA",
                            """
                            Du är en del av teamet och ska behandlas som det. Det första du gör är att få ordning på
                            miljön: kunna bygga och köra systemet lokalt, få behörigheter och veta vem du ska fråga.
                            Det tar ofta en eller två veckor, och det är helt normalt. Skriv ner varje steg du tar,
                            för då hjälper du nästa nya person och dig själv om du glömmer.

                            Ställ frågor, be om feedback och skriv ner vad du lär dig varje vecka. Det är det
                            som veckologgen är till för, och du får ett exempel innan loggen börjar.

                            ## Halvtidsutvärdering och rapport

                            Mitt i perioden träffas du, handledaren och en av oss för en halvtidsutvärdering, där
                            vi pratar om hur det går och vad som ska hända resten av tiden. Förbered dig genom att
                            fundera på vad du lärt dig, vad du vill göra mer av och om något fungerar dåligt. I slutet
                            skriver du en rapport om vad du arbetat med och lärt dig.
                            """,
                            "Din roll under LIA")
                    ])
            ],
            [
                new(
                    "Så går LIA 1 till",
                    """
                    Hej alla, det är dags att börja tänka på LIA! Perioden är **18 januari till 28 maj**, på heltid
                    på ett företag där du arbetar som utvecklare.

                    Det första steget är att hitta en plats. Läs teorin i kursen om hur du gör, och skicka in vald plats
                    **senast 20 november**. Jag finns till hands om du fastnar eller vill ha någon att bolla med.
                    """,
                    Se(2026, 10, 5, 10, 0),
                    "malin",
                    true,
                    true,
                    [
                        new("#4", "Kan man göra LIA hemifrån, eller måste det vara på plats?", 4),
                        new(
                            "malin",
                            "Ja, det går bra om företaget har rutiner för det, men tänk på att det är lättare att "
                            + "lära sig om man ibland sitter med teamet. Skriv hur det blir i uppgiften.",
                            6),
                        new("#14", "Hur många timmar per vecka är det?", 8),
                        new("malin", "Heltid, så vanlig arbetstid på företaget, ungefär 40 timmar.", 9)
                    ]),
                new(
                    "Var har ni fått plats? Dela gärna",
                    """
                    Nu har flera av er hört av sig om att ni fått plats. Det är roligt! Skriv gärna här var ni
                    hamnat och vad ni ska göra, så ser de som fortfarande söker vilka typer av företag som tar emot
                    oss. Ni behöver inte skriva namn på handledare.
                    """,
                    Se(2026, 10, 22, 11, 20),
                    "malin",
                    false,
                    true,
                    [
                        new(
                            "#2",
                            "Jag har fått plats på ett konsultbolag i Göteborg, där jag ska arbeta med ett projekt i "
                            + ".NET och Azure. Jag börjar med att sitta med en erfaren utvecklare.",
                            3),
                        new(
                            "#6",
                            "Jag hamnade hos kommunens IT-avdelning. De bygger e-tjänster för invånarna, så ganska "
                            + "mycket ASP.NET och SQL Server.",
                            4),
                        new(
                            "#9",
                            "Grattis! Jag har fått ja från ett litet produktbolag med tolv anställda. De har en app "
                            + "för fastighetsägare och jag ska få bygga en egen funktion från start.",
                            7),
                        new("#17", "Hur kom du i kontakt med dem, #9?", 8),
                        new(
                            "#9",
                            "Jag gick på ett meetup i september och pratade med en av grundarna. Sedan mejlade jag "
                            + "dagen därpå med ett litet projekt jag byggt.",
                            10),
                        new("malin", "Precis så brukar det gå till. Bra jobbat alla tre!", 12)
                    ]),
                new(
                    "Fortfarande ingen plats? Läs här",
                    """
                    Om du ännu inte har någon plats är du inte ensam, och det är fortfarande gott om tid fram till
                    20 november. Det viktigaste är att du kontaktar företag personligen och inte bara skickar
                    ansökningar. Hör av dig till mig så går vi igenom din lista, CV och mejl, och jag kan
                    ofta tipsa om företag som tagit emot studenter tidigare.

                    Skriv gärna i kommentarerna om du har kontakter att dela, eller om du vill ha en kurskamrat
                    att söka tillsammans med.
                    """,
                    Se(2026, 10, 27, 9, 30),
                    "malin",
                    false,
                    true,
                    [
                        new(
                            "#12",
                            "Jag har skickat ungefär 25 ansökningar och har bara fått nej eller ingenting alls. Vad "
                            + "gör jag fel?",
                            5),
                        new(
                            "malin",
                            "Du gör inget galet, 25 ansökningar utan svar är vanligt. Jag skulle byta taktik och "
                            + "ringa eller hälsa på hos fem företag i stället. Skicka mig ditt CV så tittar jag på det "
                            + "i morgon.",
                            7),
                        new(
                            "#3",
                            "Jag blev erbjuden en plats hos oss i Borås och kan fråga om de skulle kunna ta två. Vill "
                            + "du?",
                            9),
                        new("#12", "Ja tack, det vore snällt! Jag skickar dig mitt CV.", 10),
                        new(
                            "#18",
                            "Prova också att höra av dig till företag som gick på mässan i höstas. De skrev upp "
                            + "kontaktuppgifter på studentlistan.",
                            12),
                        new(
                            "malin",
                            "Bra tips, #18. Listan ligger i mappen Dokument i kursrummet. Och #12, boka en tid hos "
                            + "mig i veckan så går vi igenom det.",
                            14),
                        new("#12", "Tack alla, det betyder mycket!", 15)
                    ])
            ]);
    }

    private static CourseSpec Lia2()
    {
        var start = new DateOnly(2026, 8, 24);

        PartSpec WeekLog(
            int week,
            DateOnly friday,
            SubmissionPattern pattern,
            string[] answers,
            string[] feedback,
            string[] needsWork) => Assignment(
                $"Veckologg vecka {week}",
                $"""
                Skriv tre till fem meningar om vecka {week}: vad du gjort, vad du lärt dig och vad du vill ha hjälp med.

                ## Att lämna in

                - [ ] Vad du faktiskt gjort (uppgifter, inte möten)
                - [ ] Något du lärt dig
                - [ ] En sak du vill ha hjälp med eller prata om
                """,
                Se(friday, 23, 59),
                GradeScale.PassFail,
                _text,
                pattern,
                answers,
                feedback,
                needsWork);

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
                            "Praktik: Dina lärandemål under LIA",
                            """
                            Formulera tre mål som du följer upp med handledaren. Ett bra mål är konkret och går att
                            kontrollera, och det handlar om vad du ska kunna efteråt. "Lära mig mer om molnet" går inte
                            att kontrollera, men "kunna driftsätta en tjänst till Azure med en pipeline, utan hjälp"
                            går att kontrollera.

                            Välj mål från tre håll: något du redan vet att du vill utveckla (till exempel testning),
                            något företaget behöver (till exempel deras byggkedja) och något som kursen kräver.
                            Skriv sedan under varje mål hur du ska nå dit och hur du och handledaren ser att du
                            lyckats. Det är samtalet om målen som är det värdefulla, så ta med listan till första
                            handledarsamtalet och justera den därefter.

                            ## Exempel på mål

                            - Jag ska kunna skriva integrationstester mot en databas, så att jag kan göra det för en
                              ny endpoint utan mall, senast vecka 40.
                            - Jag ska ha deltagit i en kodgranskning varje vecka och kunna förklara skälen till
                              kommentarerna jag lämnar.
                            - Jag ska ha tagit ett ärende från idé till produktion tillsammans med teamet.
                            """,
                            "Dina lärandemål under LIA"),
                        Lesson(
                            "Praktik: Handledarsamtal – checklista",
                            """
                            Boka ett återkommande samtal med handledaren, helst en halvtimme varje vecka vid samma tid
                            så att det inte glöms bort. Förbered dig med tre saker: vad som gått bra, vad du fastnat
                            på och vad du vill göra härnäst. Skriv ner det ni kommer överens om och visa det nästa gång.

                            Frågor som brukar leda till bra samtal:

                            - Vad har jag gjort som fungerat bra, och vad skulle jag göra annorlunda?
                            - Vilken typ av uppgift passar mig bäst att ta nu?
                            - Finns det något jag borde kunna men inte frågar om?
                            - Vilka i teamet kan jag lära mig av under veckan?

                            Går det dåligt, till exempel att handledaren aldrig har tid, ska du inte vänta. Hör av dig
                            till
                            mig, så löser vi det tillsammans med företaget.
                            """,
                            "Handledarsamtal: checklista"),
                        Assignment(
                            "Inlämning: Individuell plan för LIA",
                            """
                            Skriv din plan för perioden. Den ska innehålla:

                            - tre lärandemål, formulerade så att de går att kontrollera
                            - för varje mål, hur du ska nå det och hur du och handledaren ser att du lyckats
                            - hur ofta och när du och handledaren träffas

                            Läs igenom teorin om lärandemål först. Planen ska vara högst en sida, och
                            ==handledaren ska ha sett den== innan du lämnar in.

                            ## Att lämna in

                            - [ ] Dina tre mål med hur du ska nå dem
                            - [ ] Tid för handledarsamtal
                            - [ ] Bekräftelse på att handledaren läst planen
                            """,
                            Se(2026, 9, 4, 23, 59),
                            GradeScale.PassFail,
                            _text,
                            SubmissionPattern.Finished,
                            [
                                "Mina mål är att kunna skriva integrationstester mot Postgres, att ta ett ärende hela "
                                + "vägen till produktion och att delta i kodgranskning varje vecka. Jag och min "
                                + "handledare "
                                + "ses varje onsdag klockan 14. Handledaren har läst planen och godkänt den.",
                                "Mål 1: förstå företagets CI/CD-kedja så att jag kan lägga till ett steg själv. Mål "
                                + "2: lära "
                                + "mig Terraform nog för att ändra en miljö. Mål 3: skriva en teknisk text som teamet "
                                + "kan använda. Vi har samtal varannan måndag.",
                                "Jag vill kunna felsöka i produktion med loggar och mätvärden, bygga en funktion med "
                                + "frontend och backend och lära mig hur teamet planerar sprintar. Handledaren tyckte "
                                + "mål 3 var för vagt, så jag har skrivit om det.",
                                "Planen har tre mål: testa mer, skriva renare kod och förstå affärsdomänen. Jag "
                                + "träffar "
                                + "handledaren varje vecka och vi kontrollerar målen i mitten av perioden."
                            ],
                            [
                                "Godkänd. Målen är konkreta och går att kontrollera, särskilt målet om att lägga "
                                + "till ett "
                                + "steg i pipelinen. Bra att handledaren har läst planen.",
                                "Godkänd. Tydlig plan och bra att ni bestämt fasta tider. Mål 3 skulle tjäna på att få "
                                + "en egen mätpunkt, till exempel antal sidor eller en granskning av teamet.",
                                "Godkänd. Bra att du tog handledarens kommentar på allvar och skrev om mål 3."
                            ],
                            [
                                "Målen är för allmänna för att kunna följas upp. 'Bli bättre på kod' går inte att "
                                + "kontrollera. Skriv om dem så att du och handledaren kan se när de är uppnådda.",
                                "Planen saknar tider för handledarsamtal och en bekräftelse från handledaren. "
                                + "Komplettera och lämna in igen."
                            ],
                            "Individuell plan för LIA")
                    ]),
                new(
                    "Löpande veckologg",
                    "En kort logg varje fredag.",
                    Se(2026, 9, 28, 7),
                    80,
                    [
                        Lesson(
                            "Praktik: Så skriver du en bra veckologg",
                            """
                            En bra logg är kort och konkret. Skriv vad du faktiskt gjort, inte bara vad du har
                            varit med om. Jämför:

                            - **Svagt:** "Jag har varit med på möten och lärt mig mycket om teamets arbetssätt."
                            - **Bra:** "Jag byggde endpointen för att avboka en bokning och skrev tre tester. I
                              granskningen fick jag kommentaren att jag inte validerade id:t, så jag lade till det.
                              Jag vill ha hjälp med hur vi hanterar migreringar i produktion."

                            Det andra exemplet visar vad du gjorde, vad du lärde dig av granskningen och vad du vill
                            ha hjälp med. Det är de tre delarna jag letar efter, och det tar fem minuter att skriva om
                            du
                            för anteckningar under veckan.

                            ## Vad händer med loggen?

                            Jag läser den och svarar med en kommentar, ofta med ett tips eller en fråga. Handledaren
                            kan se den om du vill, men du bestämmer själv. Skriv inte om kollegor på ett sätt som du
                            inte skulle säga till dem, och lämna inte ut kunduppgifter.
                            """,
                            "Så skriver du en bra veckologg"),
                        WeekLog(
                            40,
                            new DateOnly(2026, 10, 2),
                            SubmissionPattern.Finished,
                            [
                                "Den här veckan har jag byggt endpointen för att avboka en bokning och skrivit tre "
                                + "tester "
                                + "för den. I granskningen fick jag kommentaren att jag inte validerade id:t. Jag vill "
                                + "ha "
                                + "hjälp med hur vi hanterar migreringar i produktion.",
                                "Jag har satt upp utvecklingsmiljön på nytt efter att docker compose-filen ändrades, "
                                + "och "
                                + "fått en genomgång av hur teamet driftsätter. Det tog två dagar att få allt att "
                                + "fungera. Jag skulle vilja förstå var loggarna hamnar.",
                                "Veckan gick åt till att rätta en bugg i sökningen som gav dubbletter. Jag lärde mig "
                                + "använda "
                                + "`EXPLAIN` för att förstå varför frågan var långsam. Jag vill ha hjälp med att "
                                + "bedöma "
                                + "när ett index är värt det."
                            ],
                            [
                                "Bra logg. Du beskriver både vad du gjorde och vad granskningen gav dig. Prata med "
                                + "handledaren om migreringarna, det är en viktig fråga.",
                                "Godkänd. Tydlig beskrivning av miljön. Fråga gärna om du får skriva ner stegen i "
                                + "teamets wiki så att nästa person slipper två dagar.",
                                "Bra, och ett riktigt exempel på något du lärt dig. Titta på hur indexet påverkar "
                                + "skrivningar."
                            ],
                            [
                                "Loggen säger att du varit med på möten men inte vad du gjort. Skriv om den med en "
                                + "eller två "
                                + "konkreta uppgifter du arbetat med.",
                                "Loggen saknar något du vill ha hjälp med. Lägg till en eller två meningar om det."
                            ]),
                        WeekLog(
                            41,
                            new DateOnly(2026, 10, 9),
                            SubmissionPattern.Finished,
                            [
                                "Jag har suttit med i en produktionsincident och fått följa felsökningen. Det var "
                                + "lärorikt att se hur teamet kommunicerade. Nästa vecka tar jag över en liten bugg "
                                + "själv.",
                                "Jag har skrivit min första pull request som gick in utan kommentarer. Jag lärde mig "
                                + "hur "
                                + "teamet namnger grenar och commits. Jag vill ha hjälp med att dela upp större "
                                + "ändringar i mindre bitar.",
                                "Veckan handlade om att lägga till ett steg i pipelinen som kör integrationstesterna. "
                                + "Det fungerade lokalt men inte i CI, och orsaken var att databasen inte hunnit "
                                + "starta. Jag "
                                + "lade till en healthcheck."
                            ],
                            [
                                "Bra att du fick vara med. Skriv gärna vad som gjorde att kommunikationen fungerade så "
                                + "bra, det kan du ha nytta av själv.",
                                "Godkänd. Bra reflektion. Om du vill dela upp ändringarna i mindre bitar är det bra "
                                + "att "
                                + "börja med en lista över stegen innan du kodar.",
                                "Mycket bra logg: problem, orsak och åtgärd. Det är så man vill läsa dem."
                            ],
                            [
                                "Det framgår inte vad du själv bidrog med under incidenten. Komplettera med din del, "
                                + "även om den var liten."
                            ]),
                        WeekLog(
                            42,
                            new DateOnly(2026, 10, 16),
                            SubmissionPattern.Finished,
                            [
                                "Vecka 42 var halvtidsutvärderingen. Jag och handledaren gick igenom målen, och vi "
                                + "är överens om att jag ska ta mer ansvar för egna ärenden. Jag har också börjat på "
                                + "en ny funktion för notiser.",
                                "Jag har arbetat med frontend i Vue och rättat feedback från granskningen. Jag lärde "
                                + "mig "
                                + "mer om tillgänglighet efter att en kollega visade skärmläsare. Jag vill ha hjälp "
                                + "med "
                                + "hur man planerar sin tid.",
                                "Utvärderingen gick bra. Jag fick höra att jag behöver våga fråga tidigare. Resten av "
                                + "veckan skrev jag tester för kundmodulen."
                            ],
                            [
                                "Bra, och fint att ni kom överens om mer ansvar. Skriv i nästa logg hur det gick.",
                                "Godkänd. Tillgänglighet är en bra sak att lära sig tidigt. Planering tar vi gärna "
                                + "upp på nästa samtal.",
                                "Godkänd. Bra att du tar med dig kommentaren om att fråga tidigare, den ser jag fram "
                                + "emot att följa upp."
                            ],
                            [
                                "Loggen är bara en mening lång. Skriv vad du gjorde utöver utvärderingen."
                            ]),
                        WeekLog(
                            43,
                            new DateOnly(2026, 10, 23),
                            SubmissionPattern.MostlyGraded,
                            [
                                "Jag har byggt klart notiserna och lagt in dem i sprinten. Jag lärde mig att man ska "
                                + "diskutera datamodellen med teamet innan man bygger. Jag vill ha hjälp med att testa "
                                + "något som skickar mejl.",
                                "Den här veckan har jag suttit med en kollega och parprogrammerat i tre dagar. Det "
                                + "gick snabbare än jag trodde. Jag vill prata om hur man ber om hjälp utan att "
                                + "störa.",
                                "Jag har arbetat med en migrering som tog bort en kolumn i produktion. Vi gjorde det i "
                                + "två steg för att slippa driftstopp. Det var spännande."
                            ],
                            [
                                "Bra logg. För mejltester, titta på en fejkad SMTP-server som MailHog.",
                                "Godkänd. Parprogrammering är ett bra sätt att lära sig, och det du frågar efter tar "
                                + "vi gärna upp.",
                                "Bra, och precis den lärdomen jag hoppades att du skulle få om migreringar."
                            ],
                            [
                                "Det saknas något du vill ha hjälp med. Komplettera loggen med det."
                            ]),
                        WeekLog(
                            44,
                            new DateOnly(2026, 10, 30),
                            SubmissionPattern.Early,
                            [
                                "Jag har påbörjat rapporten och samlat material från de föregående loggarna. Jag "
                                + "arbetade också med en buggrättning i fakturamodulen. Jag vill ha hjälp med "
                                + "strukturen.",
                                "Veckan gick åt till att dokumentera det jag byggt, så att teamet kan ta över när jag "
                                + "är klar. Jag lärde mig hur svårt det är att skriva för någon som inte var med."
                            ],
                            [
                                "Bra logg, den visar att du tänker på överlämningen.",
                                "Godkänd. Rapportens struktur går vi igenom på nästa samtal."
                            ],
                            [
                                "Komplettera med vad du konkret har gjort i fakturamodulen."
                            ])
                    ]),
                new(
                    "Utvärdering och avslut",
                    "Halvtid, rapport och handledarens bedömning.",
                    Se(2026, 10, 12, 7),
                    40,
                    [
                        Lesson(
                            "Praktik: Halvtidsutvärderingen",
                            """
                            Halvtidsutvärderingen är ett samtal på cirka 45 minuter mellan dig, handledaren och en
                            av oss. Den är inte ett prov. Syftet är att se hur det går, att fånga upp problem medan
                            det fortfarande går att göra något åt dem och att planera resten av perioden.

                            Vi går igenom tre saker. Först dina mål: hur ligger du till, och behöver något justeras?
                            Sedan arbetssituationen: får du uppgifter som passar dig, har du den hjälp du behöver,
                            och hur fungerar handledningen? Till sist framåt: vad vill du hinna med innan perioden är
                            slut, och vad behöver företaget hjälpa till med?

                            Förbered dig genom att läsa dina veckologgar och skriva ner tre saker som gått bra, två
                            som varit svåra och en sak du önskar att företaget gjorde annorlunda. Var ärlig. Vi tar inte
                            upp något du säger med företaget utan att ni är överens om det.

                            ## Efter samtalet

                            Jag skriver en kort sammanfattning med det ni kommit överens om, och du och
                            handledaren får läsa den. Använd den som underlag när du skriver rapporten.
                            """),
                        Lesson(
                            "Praktik: LIA-rapporten",
                            """
                            Rapporten är din sammanfattning av perioden, på två till tre sidor. Skriv den så att
                            någon som inte känner företaget förstår vad du gjort. Använd den här strukturen:

                            1. **Företaget och uppdraget.** Vad gör företaget, hur ser teamet ut och vilken roll
                               hade du?
                               Max ett stycke.
                            2. **Vad jag har arbetat med.** Två eller tre konkreta uppgifter, med teknik, din del och
                               resultatet. Här kommer veckologgarna väl till pass.
                            3. **Vad jag har lärt mig.** Koppla till dina tre lärandemål: nådde du dem, och hur vet
                               du det?
                            4. **Kopplingen till kursmålen.** Välj minst tre av utbildningens mål och visa med exempel
                               hur du arbetat med dem.
                            5. **Reflektion.** Vad gick bra, vad hade du gjort annorlunda och vad tar du med dig?

                            ## Vad bedömer vi?

                            För godkänt ska rapporten innehålla alla fem delarna, vara konkret och koppla till målen.
                            För väl godkänt vill vi dessutom se en självständig reflektion: att du ser vad som
                            varit svårt och varför, inte bara att allt gått bra. Skriv med egna ord. Använder du en
                            språkmodell som hjälp ska du kunna stå för varje mening och redovisa det.
                            """),
                        Assignment(
                            "Halvtidsutvärdering",
                            """
                            Utvärderingen sker på plats tillsammans med handledaren och utbildningsledaren. Läs
                            avsnittet om halvtidsutvärderingen i förväg.

                            Ta med dig:

                            - dina lärandemål från den individuella planen
                            - dina veckologgar
                            - minst en fråga till handledaren och en till oss

                            Bedömningen är godkänd eller icke godkänd, och den baseras på att samtalet genomförts
                            och att du har en plan för resten av perioden.
                            """,
                            Se(2026, 10, 16, 16, 0),
                            GradeScale.PassFail,
                            _onSite,
                            SubmissionPattern.Finished,
                            feedback:
                            [
                                "Bra samtal. Du hade bra koll på dina mål och handledaren var nöjd. Nästa steg är "
                                + "att ta "
                                + "mer eget ansvar för ärenden, som ni kom överens om.",
                                "Godkänd. Ni har bra upplägg med fasta samtal, och planen för resten av perioden är "
                                + "tydlig.",
                                "Godkänd. Du bör fråga tidigare när du fastnar, vilket vi pratade om."
                            ]),
                        Assignment(
                            "LIA-rapport",
                            """
                            Skriv en rapport på två till tre sidor om vad du arbetat med, vad du lärt dig och hur
                            det kopplar till kursmålen. Följ strukturen i avsnittet om LIA-rapporten:

                            - företaget och uppdraget
                            - vad jag har arbetat med
                            - vad jag har lärt mig, kopplat till mina tre mål
                            - kopplingen till kursmålen
                            - reflektion

                            Rapporten ska skrivas av dig, med dina egna exempel. Använd veckologgarna som
                            underlag men skriv inte av dem. ==Namnge inte kunder och lämna inte ut hemlig kod==, och
                            kontrollera med handledaren om du är osäker på vad som får stå med.

                            Betygsskalan är godkänd, väl godkänd eller icke godkänd.

                            ## Att lämna in

                            - [ ] Rapporten som PDF eller text direkt i svaret
                            - [ ] Bekräftelse från handledaren att innehållet om företaget är okej
                            - [ ] En rad om hur du använt språkmodeller eller annan hjälp, om du gjort det

                            **Betygskriterier**

                            Godkänt kräver alla delar och en konkret koppling till minst tre av utbildningens mål.
                            För väl godkänt ska rapporten dessutom innehålla en självständig reflektion med
                            exempel på något som varit svårt och vad du gjorde åt det.
                            """,
                            Se(2026, 11, 20, 23, 59),
                            GradeScale.PassFailDistinction,
                            _text,
                            SubmissionPattern.None),
                        Assignment(
                            "Handledarens slutbedömning",
                            """
                            Handledaren bedömer ditt arbete på plats vid periodens slut, tillsammans med en av oss.
                            Handledaren får ett formulär med frågor om samarbete, självständighet, kodkvalitet och
                            hur du tagit emot feedback, och vi går igenom det tillsammans.

                            Du behöver inte göra något själv utom att boka tid med handledaren och påminna om
                            formuläret. Be om en kopia på det du får, då vet du vad du kan utveckla vidare.
                            """,
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
                    [
                        new("#5", "Räknas veckologgen om jag är sjuk en vecka?", 3),
                        new(
                            "henrik",
                            "Nej, skriv bara en rad om att du varit sjuk. Du ska ändå sjukanmäla dig till företaget "
                            + "och till mig.",
                            5)
                    ]),
                new(
                    "Halvtidsutvärderingarna är klara",
                    """
                    Nu har jag träffat alla på halvtidsutvärdering, tillsammans med era handledare. Överlag har
                    det gått bra, och många av er har tagit ansvar för riktiga ärenden. Det jag hörde mest var att
                    ni gärna ville ha mer feedback på koden, och det tar jag med mig till företagen.

                    Sammanfattningarna skickas ut den här veckan. Läs dem och ta med det ni kommit överens om i
                    veckologgarna.
                    """,
                    Se(2026, 10, 19, 14, 10),
                    "henrik",
                    false,
                    true,
                    [
                        new(
                            "#2",
                            "Tack, det var ett bra samtal. Min handledare var väldigt nöjd med att jag frågade när "
                            + "jag fastnade.",
                            4),
                        new("#10", "Min handledare var sjuk på dagen. Kan vi göra om det?", 6),
                        new("henrik", "Ja, vi hittar en ny tid. Skriv till mig så bokar vi in det i veckan.", 8)
                    ]),
                new(
                    "Tips inför LIA-rapporten",
                    """
                    Rapporten ska in **20 november**, så det är dags att börja. Några tips:

                    - Börja med att läsa igenom dina veckologgar och stryka under det du är stolt över.
                    - Skriv strukturen i teorin som rubriker först, och fyll i under dem.
                    - Be handledaren läsa avsnittet om företaget, så att inget känsligt står med.

                    Lämna gärna ett utkast till mig en vecka innan så får du kommentarer i tid.
                    """,
                    Se(2026, 10, 26, 10, 45),
                    "henrik",
                    false,
                    true,
                    [
                        new("#7", "Hur mycket får man skriva om själva koden?", 3),
                        new(
                            "henrik",
                            "Beskriv vad koden gör och hur du angrep problemet. Klistra inte in stora delar, och "
                            + "kontrollera med handledaren om något är känsligt.",
                            5),
                        new("#13", "Är det okej att skriva på engelska?", 9),
                        new(
                            "henrik",
                            "Nej, rapporten ska vara på svenska om du inte har kommit överens om något annat med mig.",
                            11),
                        new(
                            "#15",
                            "Är det ett bra exempel att koppla till kursmålen med en pull request som man är stolt "
                            + "över?",
                            20),
                        new(
                            "henrik",
                            "Ja, det är utmärkt. Beskriv vad ändringen gjorde, hur den granskades och vad du lärde "
                            + "dig av kommentarerna.",
                            22)
                    ])
            ]);
    }
}
