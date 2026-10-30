using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.DemoPeople;

namespace Backend.Infrastructure.Persistence;

internal static partial class DemoCatalog
{
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
            CSharp("NET25", new DateOnly(2025, 8, 25), new DateOnly(2025, 10, 31), "karin")
                with { Teachers = ["karin", "anders"] },
            Databases("NET25", new DateOnly(2025, 11, 3), new DateOnly(2025, 12, 19), "anders")
                with { Teachers = ["anders", "jenny"] },
            WebApps("NET25", new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 24), "larare")
                with { Teachers = ["larare", "jenny"] },
            DevOps("NET25", new DateOnly(2026, 10, 12), true, "larare")
                with { Teachers = ["larare", "karin"] },
            AiCourse("NET25", new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 18), "jenny", false)
                with { Teachers = ["jenny", "anders"] },
            Lia1() with { Teachers = ["malin"] }
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
                    new("malin", "Ja, den kommer i ett eget inlägg i kursen Programmering med C#.", 7),
                    new("#14", "Ska man ha Visual Studio eller går Rider bra?", 26),
                    new("karin", "Båda går bra, men vi visar allt i Visual Studio. Det är gratis (Community).", 28),
                    new("#14", "Tack!", 29),
                    new("elev", "Och Git? Måste man ha kunnat det innan?", 30),
                    new("malin", "Nej, vi går igenom Git från grunden i kursen.", 32)
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
                    new("malin", "Vi spelar in den och lägger länken i veckans material.", 3),
                    new("#7", "Är det verkligen torsdag 9.00? Jag har det som 9.15 i kalendern.", 20),
                    new("malin", "Bra att du frågar. Rätt är **9.00**, kalendern uppdateras under dagen.", 21),
                    new("#7", "Skönt, tack. Då hinner jag med bussen.", 22),
                    new("#18", "Kommer Anders hålla i den eller Karin?", 26),
                    new("malin", "Karin kör den, med Anders som stöd i labbet efteråt.", 27),
                    new("#2", "Tack för inspelningen, den gick bra att se i går kväll 👍", 90)
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
                [
                    new("#11", "Får man göra LIA på en annan ort än där man bor nu?", 20),
                    new(
                        "malin",
                        "Ja, det går bra. Men räkna med boende och resor "
                        + "själva, CSN-bidraget ändras inte.",
                        22),
                    new("#5", "Finns det någon lista på företag som tagit emot förut?", 26),
                    new(
                        "malin",
                        "Ja, jag lägger en lista i kursen LIA 1 senast på fredag. "
                        + "Den är inte komplett, så leta även själva.",
                        28),
                    new("#16", "Vad gäller om man redan har ett jobb som man kan göra LIA på?", 40),
                    new("malin", "Då tar vi det i ett samtal. Boka en tid via mejl.", 42),
                    new("#16", "Gör det, tack.", 43)
                ]),
            new(
                "Boka mentorssamtal innan jul",
                """
                Nu bokar jag mentorssamtal med alla i klassen. Samtalet tar **20 minuter** och handlar om hur
                studierna går, hur ni mår och vad ni vill ha hjälp med inför LIA. Du kan komma med egna frågor
                eller bara berätta hur det är.

                Tidslistan ligger i mitt rum (B214) och som delat dokument. Skriv upp dig på en tid före 20
                november. Samtalen hålls i mitt rum eller på Teams.
                """,
                Se(2026, 10, 21, 8, 50),
                "malin",
                false,
                false,
                [
                    new("#3", "Teams eller på plats, kan man välja?", 2),
                    new("malin", "Du väljer själv.", 3),
                    new("#8", "Jag har skrivit upp mig torsdag 10.40. Kan jag byta med någon?", 25),
                    new("#12", "Jag byter gärna, jag har tid 14.00 på torsdagen!", 27),
                    new("#8", "Perfekt, tack 🙂", 27),
                    new("malin", "Jag har bytt i listan.", 30)
                ]),
            new(
                "CSN: kom ihåg studieintyget",
                """
                CSN skickar ut förfrågningar om studieresultat i slutet av terminen. För att få bidrag och lån
                utbetalat nästa termin behöver ni ha **minst 75 % av poängen** godkända. Om ni är osäkra på hur ni
                ligger till: titta på er betygssida i Lärka eller prata med mig.

                Sjukdom eller andra skäl ska anmälas till CSN i förväg.
                """,
                Se(2026, 10, 30, 9, 0),
                "malin",
                false,
                false,
                [
                    new("#6", "Räknas pågående kurser med, eller bara de som är klara?", 3),
                    new(
                        "malin",
                        "Bara de som är avslutade och har betyg. DevOps-kursen "
                        + "räknas först när den är betygsatt.",
                        4),
                    new("elev", "Jag missade en tenta i våras. Påverkar det?", 7),
                    new("malin", "Boka en tid, så tittar vi på det i lugn och ro.", 8),
                    new("#15", "Kommer studieintyget i Lärka eller måste man begära det?", 24),
                    new("malin", "Du begär det själv via CSN:s e-tjänst, intyget kommer från oss automatiskt.", 26)
                ]),
            new(
                "Ny sal för AI-kursen från 16 november",
                """
                AI-kursen börjar måndag 16 november. Den hålls i **sal C305** i stället för vår vanliga sal,
                eftersom vi behöver fler uttag och en större skärm. Schemat i kalendern är uppdaterat. Dörren öppnas
                08.15 första dagen.
                """,
                Se(2026, 10, 30, 14, 30),
                "malin",
                false,
                false,
                [
                    new("#0", "C305 är väl den med de dåliga stolarna? 😅", 3),
                    new("malin", "Just den, men nya stolar kommer i december.", 4),
                    new("#9", "Går det att få en skärm till laptopen där, eller ska man ta med egen?", 6),
                    new("jenny", "Det finns skärmar och sladdar. Ta med egen laptop och laddare.", 8)
                ]),
            new(
                "AW torsdag 12 november",
                """
                Klass-AW torsdag 12 november från kl. 17.00 på Gasverket. Det är ingen obligatorisk träff, men ett
                bra tillfälle att ses efter DevOps-kursen. Skriv i kommentarerna om ni kommer, så att jag kan
                boka bord. Alkoholfritt finns, förstås.
                """,
                Se(2026, 11, 2, 8, 30),
                "malin",
                false,
                false,
                [
                    new("#4", "Jag kommer!", 2),
                    new("#13", "Vi är tre från labbgrupp B som kommer.", 3),
                    new("#1", "Går det bra att ta med sig en vän som läser ekonomi?", 5),
                    new("malin", "Absolut.", 6),
                    new("#17", "Hoppas att vi hinner vara klara med PR-granskningarna före dess 😬", 8)
                ])
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
            CSharp("NET24", new DateOnly(2024, 8, 26), new DateOnly(2024, 10, 25), "karin")
                with { Teachers = ["karin"] },
            Databases("NET24", new DateOnly(2024, 11, 4), new DateOnly(2024, 12, 20), "karin")
                with { Teachers = ["karin", "patrik"] },
            WebApps("NET24", new DateOnly(2025, 3, 3), new DateOnly(2025, 4, 25), "jenny")
                with { Teachers = ["jenny", "patrik"] },
            DevOps("NET24", new DateOnly(2026, 3, 2), false, "patrik")
                with { Teachers = ["patrik", "karin"] },
            AiCourse("NET24", new DateOnly(2026, 4, 13), new DateOnly(2026, 5, 15), "jenny", true)
                with { Teachers = ["jenny"] },
            Lia2() with { Teachers = ["henrik"] }
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
                    6),
                new("#10", "Måste handledaren läsa den?", 20),
                new("henrik", "Nej, men du kan visa den om du vill. Jag läser den alltid.", 22),
                new("#10", "Okej, då skriver jag lite mer ärligt 😄", 23),
                new("#6", "Kan jag skriva loggen i telefonen på tåget?", 28),
                new("henrik", "Ja, det går bra. Det är formuläret i Lärka som gäller.", 30)]),
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
                [
                    new("#12", "Kommer du till oss i Kista eller kan det vara på Teams?", 5),
                    new("henrik", "Kista, jag kommer dit på tisdagen. Annars Teams.", 6),
                    new("#12", "Tack, vi har en lunchpaus 12–13 om det passar.", 7),
                    new("#0", "Jag har dig på torsdag 10.00. Stämmer det?", 28),
                    new("henrik", "Ja, men flytta det en kvart framåt, jag har ett möte före.", 30),
                    new("#0", "Noterat.", 30)
                ]),
            new(
                "CSN och studieintyg inför våren",
                """
                För att få CSN nästa termin behöver ni som är inne på LIA visa att ni deltar. Jag skickar
                aktivitetsintyg i slutet av november. Om ni tänker byta praktikplats eller sjukskriva er mitt i
                perioden: hör av er till mig **före**, inte efter.
                """,
                Se(2026, 10, 30, 10, 20),
                "henrik",
                false,
                false,
                [
                    new("#9", "Vad händer om min handledare är sjuk en vecka?", 4),
                    new(
                        "henrik",
                        "Då är du ledig eller får jobba med någon annan på "
                        + "företaget. Kolla med företaget först.",
                        5),
                    new("#2", "Räknas LIA som heltid för CSN?", 9),
                    new("henrik", "Ja, så länge du arbetar 40 timmar i veckan.", 10)
                ]),
            new(
                "AW för .NET24 fredag 20 november",
                """
                Efter besöken på LIA-platserna vill jag gärna ses och höra hur det går. Fredag 20 november
                kl. 16.30 har vi AW i skolans café. Ta med en kollega från LIA om ni vill, så kan ni prata
                med varandra. Jag bjuder på fika.
                """,
                Se(2026, 11, 2, 9, 5),
                "henrik",
                false,
                false,
                [
                    new("#4", "Är det på riktigt att det bjuds på fika? 🙂", 2),
                    new("henrik", "Ja, men bara en bulle per person.", 3),
                    new("#4", "Jag kommer!", 3),
                    new("#14", "Kommer ni som slutade i våras också?", 6),
                    new("henrik", "Ja, de är inbjudna.", 7)
                ]),
            new(
                "Veckologg: ny mall från 9 november",
                """
                Från och med 9 november finns en ny mall för veckologgen med tre fasta frågor: vad har du gjort,
                vad har du lärt dig och vad vill du fråga mig om. Gammal mall funkar fortfarande veckan ut. Det
                går lika fort som förut.
                """,
                Se(2026, 11, 2, 11, 45),
                "henrik",
                false,
                false,
                [
                    new("#7", "Betyder det att man måste skriva något om det man vill fråga om varje vecka?", 3),
                    new("henrik", "Nej, lämna tomt om du inte har någon fråga. Men skriv gärna något.", 4)
                ])
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
        [
            FiberSplicing() with { Teachers = ["mikael", "camilla"] },
            FiberPon() with { Teachers = ["roger", "camilla"] },
            FiberLia() with { Teachers = ["sofie"] }
        ],
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
                    8),
                new("#3", "Går det bra att ta med egen fiberlupp?", 24),
                new(
                    "sofie",
                    "Nej, ingen egen utrustning. Allt som lyser eller "
                    + "förstorar ska vara godkänt av verkstaden.",
                    26),
                new("#3", "Fair, jag hade inte tänkt på lasern.", 27),
                new("#15", "Finns det omklädningsrum?", 30),
                new("sofie", "Ja, och skåp. Ta med eget hänglås.", 32)]),
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
                    4),
                new("#12", "Hur lång tid tar det dit?", 6),
                new("mikael", "Ungefär 40 minuter. Ta med något att äta, det finns ingen affär nära.", 7),
                new("#12", "Tack. Kan man ta med fika till bussen?", 8),
                new("mikael", "Ja, men ingen kaffekopp i handen när vi kör, ni slänger er inte på varandra 😄", 9),
                new("#8", "Vilka kläder gäller? Arbetskläder, eller vanliga och byta på plats?", 20),
                new("mikael", "Arbetskläder och skor redan i bussen. Byt hemma.", 21),
                new("#8", "Tack!", 21)]),
            new(
                "Mentorssamtal vecka 45",
                """
                Det är dags för mentorssamtal. Jag tar 20 minuter med var och en under vecka 45, i mitt rum eller
                på Teams. Vi pratar om hur det går på kurserna, om LIA och om det ni känner att ni behöver.
                Skriv upp er på listan som hänger vid verkstadens dörr.
                """,
                Se(2026, 10, 29, 12, 0),
                "sofie",
                false,
                false,
                [
                    new("#2", "Måste man komma om det går bra?", 3),
                    new("sofie", "Ja, det är ju då det är lättast att prata om hur man vill ha det framöver.", 4),
                    new("#2", "Okej, jag skriver upp mig.", 5),
                    new("#16", "Torsdag 15.00 fungerar inte för mig. Finns det något på morgonen?", 20),
                    new("sofie", "Ja, torsdag 8.15 är ledig. Jag skriver dig där.", 22)
                ]),
            new(
                "Ny sal för teorin: B12",
                """
                Från och med nästa vecka hålls alla teorilektioner i **sal B12** i stället för B4. B4 behövs
                för en annan utbildning. Verkstaden och mätrummet är oförändrade. Sal B12 ligger precis intill
                trapphuset, så det är lätt att hitta.
                """,
                Se(2026, 10, 30, 13, 45),
                "mikael",
                false,
                false,
                [
                    new("#10", "Finns det plats för 18 personer?", 3),
                    new("mikael", "Ja, 24 platser.", 4),
                    new("#10", "Bra. Kan man ta med sig sin egen kaffemugg?", 5),
                    new("mikael", "Ja. Det är bara instrumenten som inte får vara nära vätska.", 6),
                    new("#13", "Rättelse: det är B12 med 12, inte B21. Jag gick fel i går 😅", 24)
                ]),
            new(
                "AW efter fältdagen",
                """
                Efter fältdagen på torsdag 5 november tar vi en gemensam middag på pizzerian vid stationen. Det är
                frivilligt. Skriv i kommentarerna om du kommer och om du är allergisk mot något.
                """,
                Se(2026, 11, 2, 12, 10),
                "sofie",
                false,
                false,
                [
                    new("#1", "Jag kommer! Vegetarian.", 2),
                    new("#7", "Jag kommer, men måste åka 18.30.", 3),
                    new("sofie", "Bra, då bokar jag bord kl. 17.30.", 5),
                    new("#14", "Kan man fråga om vi får äta i bussen?", 6),
                    new("mikael", "Nej 🙂", 7)
                ])
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
                    new("malin", "Noterat, båda punkterna kommer med.", 8),
                    new("#0", "Kan vi också ta upp att WiFi tappar i sal B4?", 20),
                    new("malin", "Ja, jag lägger till det och pratar med IT i förväg.", 22),
                    new("#2", "Tack. Har ni något förslag på vem som skriver protokoll?", 26),
                    new("#1", "Jag kan göra det den här gången.", 27),
                    new("malin", "Perfekt, tack!", 28)
                ]),
            new(
                "Protokoll från klassrådet 8 oktober",
                """
                **Närvarande:** Malin (ordförande), tre elevrepresentanter och Roger (adjungerad).

                **1. Utvärdering av C#-kursen.** Eleverna tyckte kursen var bra upplagd men att labbarna i
                slutet var för stora. Önskemål om fler övningsuppgifter utan betyg. *Åtgärd:* Karin tittar på
                uppdelningen av labbarna i kommande kurser.

                **2. Datorsalen.** Salen blir för varm på eftermiddagarna. *Åtgärd:* Malin kontaktar fastighets-
                ansvarig.

                **3. Övrigt.** Fråga om kaffe i korridoren. *Beslut:* Frågan lyfts till skolledningen.

                Nästa klassråd: 5 november.
                """,
                Se(2026, 10, 9, 14, 0),
                "malin",
                false,
                false,
                [
                    new(
                        "#1",
                        "Tack för protokollet! En liten rättelse: vi sa att "
                        + "labbarna kunde delas upp i två, inte tre.",
                        4),
                    new("malin", "Rättat, tack.", 5),
                    new("#0", "Vad hände med kaffefrågan?", 28),
                    new("malin", "Skolledningen har sagt att de tittar på det. Jag uppdaterar på nästa klassråd.", 30),
                    new("#0", "Alltså inget kaffe 😂", 31)
                ]),
            new(
                "Svar om värmen i datorsalen",
                """
                Fastighetsansvarig har svarat. Ventilationen i datorsalen justeras vecka 46 och i väntan på det
                kan ni öppna fönstren i korridoren. Om det är över 26 grader ska ni säga till mig, så meddelar jag
                fastighetsansvarig igen.
                """,
                Se(2026, 10, 30, 16, 0),
                "malin",
                false,
                false,
                [
                    new("#1", "Tack för snabb återkoppling!", 3),
                    new("elev", "Det var 27 grader i salen i dag vid halv tre.", 20),
                    new("malin", "Tack, jag skickar det vidare.", 22),
                    new("#2", "Kan vi låna fläktar i väntan?", 24),
                    new("malin", "Jag frågar.", 25)
                ]),
            new(
                "Önskemål inför AI-kursen: sammanställning",
                """
                Jag har samlat in önskemålen inför AI-kursen som börjar 16 november. Mest efterfrågat: tydliga
                regler för hur AI-verktyg får användas i inlämningar, tillgång till Azure-krediter och mer tid
                för egna projekt. Jenny har fått listan och återkommer på klassrådet 5 november.
                """,
                Se(2026, 11, 2, 10, 40),
                "malin",
                false,
                false,
                [
                    new("elev", "Bra! Jag saknade en punkt: kan vi få veta hur betygen sätts redan första veckan?", 3),
                    new("malin", "Bra poäng, jag lägger till den på agendan.", 4),
                    new("#0", "Vi tar upp det på torsdag.", 5)
                ])
        ]);
}
