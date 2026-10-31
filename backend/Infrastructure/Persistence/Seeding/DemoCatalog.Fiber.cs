using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

internal static partial class DemoCatalog
{
    // --- Fibertekniker: praktiskt arbete, bedömning på plats ---

    private static CourseSpec FiberSplicing() => new(
        "FIB25_SKARV",
        "Skarvning och mätning",
        "Säkert arbete med fiber, smältskarvning och mätning med OTDR. Mycket av kursen är praktisk och bedöms "
        + "på plats i verkstaden.\n\n"
        + "**Efter kursen kan du**\n\n"
        + "1. arbeta säkert med fiber, laser och kemikalier\n"
        + "2. förbereda, klyva och smältskarva enkelmodsfiber med låg dämpning\n"
        + "3. mäta en länk med OTDR, tolka kurvan och skriva ett mätprotokoll",
        40,
        new DateOnly(2025, 10, 6),
        new DateOnly(2026, 1, 30),
        "mikael",
        [
            Week(
                "Vecka 1 – Säkerhet, laser och riskbedömning",
                "Laser, glassplitter, kemikalier och arbetsmiljö.",
                new DateOnly(2025, 10, 6),
                [
                    Lesson(
                        "Teori: Laserklasser och ögonskydd",
                        """
                        All kommunikation i fibernät sker med infraröd laser, och ljuset syns inte. Det är det som
                        gör den farlig: ögat reagerar inte med blinkreflex eftersom du inte ser något, och ljuset
                        fokuseras på näthinnan precis som synligt ljus. Skador är permanenta och märks ofta inte
                        förrän det är försent.

                        De flesta sändare i accessnät är laserklass 1 eller 1M, men en OLT, en förstärkare eller en
                        OTDR kan vara klass 3B. Regeln är därför enkel: **titta aldrig in i en fiber eller en
                        kontakt som kan vara ansluten**, och titta aldrig på en fiberände med lupp eller mikroskop
                        innan du vet att länken är avstängd. Ett fibermikroskop förstorar ljuset lika mycket som
                        bilden.

                        Vi använder två skydd. Det första är rutinen: stäng av eller koppla bort sändaren, sätt
                        dammkåpor på alla kontakter och kontrollera med en effektmätare att fibern är mörk. Det andra
                        är skyddsglasögon, men tänk på att glasögon bara skyddar mot vissa våglängder. De ersätter
                        aldrig rutinen.

                        Använd en synlig felsökningslaser (VFL, röd, 650 nm) för att följa fibrer och hitta brott.
                        Den är också laser och ska inte riktas mot ansikten. Tänk på att ljus från en VFL kan lysa
                        genom kabelmanteln, vilket är hela poängen när man söker ett brott.

                        ## Laser i praktiken

                        Ni kommer att få en gul lapp av oss på första verkstadsdagen: *Laser i verkstaden*. Den
                        gäller hela utbildningen. Kort version: ingen aktiv utrustning kopplas in utan att
                        läraren vet om det, skyddsglasögon sitter på när vi kör OTDR, och vi pekar aldrig med en
                        fiberände på kamrater, inte ens "bara för skojs skull". Det sista säger vi varje år
                        eftersom någon alltid undrar.
                        """,
                        "Laserklasser och ögonskydd"),
                    Lesson(
                        "Teori: Fibersmulor, kemikalier och ordning på arbetsplatsen",
                        """
                        Den farligaste biten fiber är den som du inte ser. När du klyver eller avisolerar blir det
                        små glasbitar, bara någon millimeter långa, som är vassa som nålar. Hamnar de i fingret
                        eller ögat sitter de kvar och går svåra att hitta. Därför har varje arbetsplats en
                        **fibersmulsburk** med lock, som ska stå inom räckhåll, och därför lägger vi aldrig bitar
                        på bordet, i fickan eller i papperskorgen.

                        Några vanor som håller dig hel: ät och drick aldrig vid skarvbordet, torka inte ansiktet med
                        handen, borsta aldrig av dig med handen utan använd en tejpbit, och tvätta händerna när du
                        är klar. Har du fått en splitter i huden ska du tala om det för läraren, så hjälper vi dig
                        att ta bort den med pincett och förstoringsglas.

                        Kemikalierna är få men ska hanteras rätt. Isopropanol (IPA, minst 99 %) används för
                        rengöring av fibern och är brandfarlig. Vissa gelrengöringsmedel för kabelfyllning är
                        irriterande för hud och ögon. Alla kemikalier har ett **säkerhetsdatablad**, och det ska
                        finnas att läsa på arbetsplatsen. Vi går igenom hur man läser det: farosymboler, första
                        hjälpen och hur avfallet tas om hand.

                        Skarvaren värms upp till flera hundra grader när den smälter fibern och krymper
                        skarvskyddet. Rör inte värmeugnen direkt efter en skarv, och låt aldrig apparaten stå
                        obevakad påslagen. Ordning på arbetsplatsen är ett säkerhetskrav, inte en ordningsfråga: en
                        sladd som ligger i vägen är en risk för både dig och dyr utrustning.

                        ## Ordning i praktiken

                        Varje pass avslutas på samma sätt. Smulsburken töms av den som har veckans städuppdrag,
                        bänken torkas av med IPA och en luddfri duk, skarvaren får sin dammkåpa och verktygen
                        läggs tillbaka i sin låda. Det tar fem minuter och är en del av bedömningen.
                        """,
                        "Skärande avfall och kemikalier"),
                    Exercise(
                        "Övningsuppgift: Hitta riskerna i verkstaden",
                        """
                        För att öva på att se risker innan de blir olyckor ska ni gå en runda i fiberverkstaden
                        tillsammans två och två. Ta med ett papper och en penna, inga telefoner vid skarvborden.

                        Hitta minst sex saker som skulle kunna gå fel under ett vanligt skarvpass. Det kan vara
                        både saker som är rätt, men svåra att komma ihåg, och saker som faktiskt är fel, till
                        exempel en sladd över golvet eller en burk utan lock. Sortera dem i tre grupper:

                        - risk för ögon eller hud (laser, glassplitter, kemikalier)
                        - risk för brand eller värme (IPA, skarvaren, värmeugn)
                        - risk för fall, kläm eller snubbel (stege, sladdar, verktyg)

                        Välj sedan ut den risk som ni tycker är allvarligast och skriv en mening om vad ni skulle
                        göra för att ta bort den, inte bara skydda er mot den. Vi går igenom svaren tillsammans i
                        början av nästa pass.
                        """,
                        _text),
                    Assignment(
                        "Inlämning: Egenkontroll: riskbedömning",
                        """
                        Gör en riskbedömning för ett tänkt arbetsplatsbesök. Utgå från att du ska skarva fiber i
                        en teknikbod vid en landsväg, tillsammans med en kollega, en eftermiddag i november.

                        Beskriv **tre risker** som du kan komma att möta, och för varje risk:

                        - vad som kan hända och hur allvarligt det är
                        - vad du gör för att förebygga det (ta bort risken, avskärma, skydda)
                        - vem du ska kontakta om det ändå sker

                        Skriv också i en egen rad vad du gör först om någon i teamet skadar ögonen med laser eller
                        får en glassplitter i ögat. Använd gärna de riskbedömningsmallar som vi gick igenom på
                        lektionen, men skriv med egna ord.

                        💡 Tips: Det är lätt att välja laserrisken eftersom vi pratat så mycket om den. Titta
                        även på det som är vardag: vägarbetsplatsen, kylan och att man är trött efter en lång dag.
                        Där händer de flesta olyckorna.

                        ## Att lämna in

                        - [ ] En text med tre risker, åtgärder och kontaktperson för var och en
                        - [ ] En rad om vad du gör först vid ögonskada

                        **Betygskriterier**

                        Uppgiften visar kursmål 1: att arbeta säkert med fiber, laser och kemikalier. För godkänt
                        ska riskerna vara realistiska, åtgärderna konkreta och kontaktvägarna korrekta (112 vid
                        akut fara, därefter arbetsledare).
                        """,
                        Friday(new DateOnly(2025, 10, 6), 1),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Risker: ögonskada av laser, skärskador av fibersmulor, fall från stege. Åtgärder: "
                            + "skyddsglasögon, avfallsburk vid arbetsplatsen, stege med kontrollerad fotplatta. "
                            + "Vid olycka ringer jag 112 och sedan arbetsledaren.",
                            "1. Glassplitter i ögat: jag sätter på mig glasögon innan avisolering och kör alltid med "
                            + "smulsburk. 2. Laser: jag kontrollerar att fibern är mörk med effektmätare innan jag "
                            + "tittar i en kontakt. 3. Halka i november: jag har halkskydd på skorna och sopar bort "
                            + "löv från trappan. Kontakt: 112, därefter arbetsledaren och skyddsombudet. Vid "
                            + "ögonskada spolar jag inte med vatten utan ringer 1177 och söker vård direkt.",
                            "Jag tänker mig en teknikbod vid vägen. Risk 1: trafik när jag lastar ur, åtgärd "
                            + "varselväst och parkera så att dörren öppnas bort från vägen. Risk 2: kyla, åtgärd "
                            + "varma kläder och pauser. Risk 3: fibersmulor, åtgärd burk med lock. Vid olycka "
                            + "kontaktar jag arbetsledaren och larmar 112 vid allvarlig skada.",
                            "Tre risker: laser (stänger av sändaren och kontrollerar mörk fiber), IPA nära "
                            + "skarvaren (brandfarligt, förvaras i stängd flaska och inte vid värmeugnen), "
                            + "elektriska skarvaren i fuktig miljö (jordad kontakt, torrt underlag). Akut: 112, "
                            + "sedan arbetsledare. Jag skulle även kolla säkerhetsdatabladet för IPA."
                        ],
                        [
                            "Bra, realistiska risker och tydliga åtgärder. Godkänd.",
                            "Godkänd. Bra att du tog med vardagsriskerna och inte bara lasern.",
                            "Fullt godkänd, och jag gillar kopplingen till säkerhetsdatabladet."
                        ],
                        null,
                        "Egenkontroll: riskbedömning")
                ],
                "Säkerhet vid fiberarbete"),
            Week(
                "Vecka 5 – Klyvning och smältskarvning",
                "Förbereda, klyva och skarva med låg dämpning.",
                new DateOnly(2025, 11, 3),
                [
                    Lesson(
                        "Teori: Avisolering, rengöring och klyvning",
                        """
                        En bra skarv börjar långt innan skarvaren. Nästan alla dåliga skarvar, över 0,1 dB, beror
                        på smuts eller en dålig klyvning, inte på skarvaren. Därför lägger vi mer tid på
                        förberedelsen än på själva smältningen.

                        Fibern har ett skyddande lager av lack (250 µm, "coating") som ska bort innan
                        skarvning, men glaset under (125 µm) ska förbli orört. Avisolera med ett
                        avisoleringsverktyg i en jämn rörelse, rakt ut från
                        kroppen. Torka av det blottade glaset med en luddfri duk fuktad med IPA, **en gång** och i
                        en riktning. Torka inte fram och tillbaka, då drar du bara smutsen över fibern.

                        Klyvning innebär att man ritar en mikroskopisk repa i glaset och drar, så att fibern
                        brister helt rakt. Klyvvinkeln ska vara mindre än en grad; de flesta klyvverktyg visar
                        vinkeln i skarvaren, och skarvaren avbryter om den är för dålig. Klyvverktygets kniv har
                        flera positioner, och den bör roteras med jämna mellanrum. Du känner när kniven är slö:
                        klyvningen blir ojämn eller så går fibern sönder.

                        När du har klyvt får fibern **inte** röra något. Inte bordet, inte fingret och inte en
                        annan fiber. Lägg den direkt i skarvaren, och om du tappar den klyver du om. Det lönar sig
                        alltid.

                        Fiberänden som blir över ska direkt i smulsburken. Det är lätt att glömma när man är
                        stressad, och det är också då man får en splitter i handen.

                        ## Förberedelse i praktiken

                        Lär dig ordningen utantill: kabelmantel av, buffert av, rengör, klyv, skarvskyddet på
                        före skarvning (glöm inte att trä det på fibern först, det är den vanligaste missen),
                        skarva, skyddet i värmeugnen, kontrollera. Ni kommer att öva ordningen tills ni gör den i
                        sömnen. Det är det som gör det snabbt på riktiga jobb.
                        """,
                        "Rengöring och klyvning"),
                    Lesson(
                        "Teori: Smältskarvning steg för steg",
                        """
                        En smältskarvare riktar upp två fiberändar med hjälp av kameror, smälter dem samman med en
                        elektrisk ljusbåge och skjuter ihop dem. De flesta moderna skarvare ställer in fibrerna
                        efter kärnan (core alignment) eller efter manteln (cladding alignment). Det senare går
                        fortare, men ger lite högre dämpning om kärnan sitter excentriskt.

                        Skarvaren väljer ett skarvprogram efter fibertyp (SM, G.652 eller G.657) och visar
                        slutresultatet som en **uppskattad dämpning i dB**. Ett bra värde för enkelmodsfiber är
                        under 0,05 dB, och vårt mål är under 0,1 dB. Värdet är en uppskattning från bilden och
                        stämmer inte alltid med vad OTDR:n visar, så det kan skilja några hundradelar.

                        Skarvaren kontrollerar även dragprovet, ett mjukt drag på ungefär 2 N som visar att
                        fibern inte kommer loss. Därefter flyttas skarven till värmeugnen, där ett skarvskydd (som
                        består av en krympslang, ett stödjande stålstift och ett smältlim) krymps till en stabil
                        enhet. Skyddet är 40 eller 60 mm långt beroende på kassett, och ska passa i bricka och
                        organizer.

                        Om dämpningen blir för hög, gör **inte** om skarven direkt på samma ställe. Kontrollera
                        först: var fibrerna rena? Var klyvningen bra? Är elektroderna slitna? Skarvaren visar ofta
                        ett felmeddelande som berättar vad som är fel, till exempel "dålig klyvning" eller
                        "kärnan inte hittad". Läs det, åtgärda och skarva om. Att skarva om samma fiber tre gånger
                        betyder oftast att något annat är fel.

                        ## Skarvning i praktiken

                        Skriv ned dämpningsvärdet för varje skarv i ert skarvprotokoll, tillsammans med fibernummer
                        och färg. Det är protokollet som en kollega har framför sig när ni lämnar över en
                        anläggning, så skriv så att andra kan läsa det.
                        """,
                        "Smältskarvning steg för steg"),
                    Exercise(
                        "Övningsuppgift: Läs av skarvaren",
                        """
                        Innan ni skarvar på riktigt ska ni öva på att läsa av skarvarens värden. Läraren visar
                        fyra skarvresultat på projektorn. För varje resultat ska ni, två och två, avgöra:

                        - Är skarven godkänd (under 0,1 dB) eller ska den göras om?
                        - Om den ska göras om: vad är den mest sannolika orsaken? Välj bland smutsig fiber, dålig
                          klyvning, slitna elektroder eller fel skarvprogram.
                        - Vad gör ni först, innan ni skarvar om?

                        Anteckna svaren på ett papper. Vi går igenom dem muntligt, och det finns oftast mer än ett
                        rimligt svar. Det viktiga är att du kan förklara varför.

                        Räkna också ut vad en länk med 8 skarvar som alla håller 0,08 dB bidrar med i total
                        dämpning. Jämför med en länk där åtta skarvar håller 0,03 dB. Hur mycket är skillnaden, och
                        spelar den roll i en dämpningsbudget på 28 dB?
                        """),
                    Assignment(
                        "Praktik: Skarvprov på plats",
                        """
                        Skarvprovet genomförs i verkstaden inför läraren. Du skarvar **tolv fibrar** i en
                        skarvkassett och ska klara alla under 0,1 dB. Provet tar cirka 90 minuter och bedöms medan
                        du arbetar, inte efteråt.

                        Det här kontrolleras vid bänken:

                        - [ ] Skyddsglasögon på, smulsburken på plats och bänken ren innan du börjar
                        - [ ] Skarvskyddet är trätt på före skarvning, på samtliga tolv fibrar
                        - [ ] Rengöring och klyvning enligt rutinen. Läraren tittar på ordning och teknik
                        - [ ] Dämpning under 0,1 dB på varje skarv (vi läser av skarvaren tillsammans)
                        - [ ] Dragprovet godkänt och skarvskydden är raka och utan luftbubblor
                        - [ ] Kassetten är lagd utan snäva böjar, fibrerna ligger ordnade och märkta
                        - [ ] Verktyg tillbaka, bänken avtorkad, avfallet tömt

                        ==Alla tolv skarvar ska vara under 0,1 dB== för godkänt. Får du över 0,1 dB får du göra om
                        skarven, men max tre försök per fiber. Behöver du fler försök noterar vi att du behöver
                        öva mer och gör ett nytt tillfälle.

                        💡 Tips: Ta det lugnt. De som gör sämst ifrån sig är de som skyndar för att hinna klart.
                        Det kommer inte att gå snabbare om du skarvar om tre gånger.

                        **Betygskriterier**

                        Provet visar kursmål 2: att förbereda, klyva och smältskarva enkelmodsfiber med låg
                        dämpning. **G**: alla skarvar under 0,1 dB, säkert arbetssätt och ordning. **VG**: dessutom
                        i genomsnitt under 0,05 dB, klart inom tiden utan omskarvning, och att du själv kan
                        förklara vad du gjorde när en skarv blev dålig.
                        """,
                        Friday(new DateOnly(2025, 11, 3), 3),
                        GradeScale.PassFailDistinction,
                        _onSite,
                        SubmissionPattern.Finished,
                        null,
                        [
                            "Godkänd. Alla tolv skarvar under 0,1 dB och en lugn, ordningsam arbetsgång.",
                            "Bra jobbat: snygga skarvskydd och tydligt märkta fibrer. Godkänd.",
                            "Godkänd. Du tappade tid på klyvningen i början, men rättade till det själv."
                        ],
                        null,
                        "Skarvprov på plats")
                ],
                "Smältskarvning"),
            Week(
                "Vecka 9 – OTDR-mätning och mätprotokoll",
                "Mäta en länk, tolka kurvor och dokumentera.",
                new DateOnly(2025, 12, 1),
                [
                    Lesson(
                        "Teori: OTDR-mätning och tolkning",
                        """
                        En OTDR (Optical Time Domain Reflectometer) skickar korta ljuspulser in i fibern och mäter
                        hur mycket ljus som kommer tillbaka. Resultatet är en kurva som visar hur länken ser ut
                        från sändaren till änden: dämpningen per kilometer, varje skarv, varje kontakt och eventuella
                        brott. Det är fiberteknikerns viktigaste mätinstrument.

                        Kurvan lutar neråt från vänster till höger, eftersom ljuset dämpas på vägen. Lutningen är
                        dämpningskoefficienten, normalt cirka 0,35 dB/km vid 1310 nm och 0,2–0,25 dB/km vid 1550
                        nm. Skarvar syns som små steg nedåt (en bra skarv är under 0,1 dB), kontakter och brott
                        syns som en topp som reflekterar ljus, och slutet av fibern syns som en kraftig reflektion
                        följd av brus.

                        Några inställningar styr mätningen. **Pulsbredd** avgör hur långt in du ser och hur bra
                        du kan skilja nära händelser åt: kort puls ger bra upplösning men kort räckvidd. **Mätområde**
                        ska vara lite längre än länken. **Medelvärdesbildning** (tid) ger en lugnare kurva. Och
                        kom ihåg **startfibern**: den första kontakten och den sista ligger i OTDR:ns döda zon, så
                        man kopplar en startfiber (och en slutfiber) på 500 m eller mer för att kunna mäta dem.

                        Mät alltid i **båda riktningarna** och räkna medelvärdet för varje skarv. En skarv kan se
                        ut som en förstärkning (negativ dämpning) från ena hållet, beroende på att fibrerna har olika
                        modfältsdiameter, och det rättar sig i medelvärdet.

                        ## Mätprotokoll i praktiken

                        Ett mätprotokoll ska kunna läsas av någon som inte var med. Skriv länkens namn och längd,
                        våglängd, pulsbredd, datum, vem som mätte, instrumentets serienummer, total dämpning,
                        dämpning per skarv och kontakt samt en kort kommentar till allt som avviker. Kurvfilerna
                        (.sor) sparas tillsammans med protokollet.
                        """,
                        "OTDR-mätning och tolkning"),
                    Exercise(
                        "Övningsuppgift: Tolka en OTDR-kurva",
                        """
                        För att öva på att läsa kurvor får ni en utskrift av en OTDR-mätning från en länk på cirka
                        4 km, mätt vid 1550 nm. Arbeta två och två och svara på frågorna på utskriften.

                        - Hur lång är länken, och var sitter skarvarna?
                        - Vilken skarv har högst dämpning, och är den godkänd (under 0,1 dB)?
                        - Det finns en reflekterande händelse vid cirka 2,1 km. Vad är det troligast, en kontakt
                          eller ett brott? Hur kan du avgöra det?
                        - Räkna ut dämpningskoefficienten i dB/km mellan 0,5 km och 3,5 km, och jämför med det
                          förväntade värdet på 0,22 dB/km.

                        Ni får lämna in svaren som text om ni vill ha kommentarer från läraren.
                        """,
                        _text),
                    Assignment(
                        "Inlämning: Mätprotokoll från fältövning",
                        """
                        Under fältövningen mäter vi en länk på cirka tre kilometer med OTDR. Du fyller i ett
                        mätprotokoll och skickar in det här. Mallen delas ut på plats och du kan skriva den i text,
                        som en tabell eller som en punktlista.

                        Protokollet ska innehålla:

                        - länkens namn, längd och våglängd (vi mäter vid både 1310 och 1550 nm)
                        - total dämpning
                        - dämpning per skarv och per kontakt, och om de ligger inom gränsvärdena
                        - en kommentar för **varje avvikelse**, med vad du gjorde åt den

                        Om en kontakt eller skarv ligger över gränsvärdet ska du rengöra, mäta om och redovisa
                        både före- och eftervärde. Det är det som visar att du förstår mätningen och inte bara
                        skriver av siffror.

                        💡 Tips: Skriv hellre ett värde för mycket än ett för lite. Och glöm inte att skriva
                        vilken riktning du mätte i.

                        ## Att lämna in

                        - [ ] Ifyllt mätprotokoll med länk, våglängd och dämpning per händelse
                        - [ ] Kommentar och omätning för varje avvikelse
                        - [ ] Kurvfilen (namnet på .sor-filen räcker)

                        **Betygskriterier**

                        Uppgiften visar kursmål 3: att mäta en länk med OTDR, tolka kurvan och skriva ett
                        mätprotokoll. **G**: komplett protokoll med korrekta värden. **VG**: dessutom mätning i
                        båda riktningarna med medelvärde per skarv och en tydlig analys av varje avvikelse.
                        """,
                        Friday(new DateOnly(2025, 12, 1), 2),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Länk 3,02 km, våglängd 1550 nm. Total dämpning 1,1 dB. Skarvar: 0,03, 0,05, 0,04 dB. "
                            + "Ingen avvikelse, men kontakten vid änden hade 0,6 dB, så den rengjordes och mättes om "
                            + "till 0,3 dB.",
                            "Länk 2,96 km, 1310 och 1550 nm, mätt från båda håll. Total dämpning 1,3 dB vid 1310 "
                            + "och 0,8 dB vid 1550. Skarv 2 visade -0,02 dB från A och 0,09 från B, medelvärde "
                            + "0,035 dB. Ingen avvikelse efter medelvärdesbildning.",
                            "Länk 3,1 km, 1550 nm, pulsbredd 100 ns. Total dämpning 1,2 dB. Tre skarvar, högst "
                            + "0,07 dB. Avvikelse: kontakt 1 med 0,55 dB, rengjord med IPA och mätt om till 0,25 dB.",
                            "Mätte 3,0 km vid 1550 nm. Total dämpning 1,0 dB, skarvar 0,02 till 0,06 dB. Jag "
                            + "glömde startfiber vid första mätningen, så första kontakten hamnade i döda zonen. "
                            + "Kopplade startfiber och mätte om."
                        ],
                        [
                            "Tydligt protokoll och bra att du redovisar både före- och eftervärde. Godkänd.",
                            "Väl genomfört. Medelvärde från båda riktningarna visar att du förstått varför "
                            + "man mäter åt båda håll. VG.",
                            "Godkänd. Fyll gärna i serienummer på instrumentet nästa gång."
                        ],
                        null,
                        "Mätprotokoll från fältövning")
                ],
                "Mätning med OTDR")
        ],
        [
            new(
                "Första verkstadsdagen: skyddsglasögon och arbetsskor",
                """
                Måndag klockan 8.30 ses vi i fiberverkstaden. Ta med **skyddsglasögon**, **arbetsskor** (inga
                sneakers, vi har tunga lådor) och långärmad tröja. Ingen laptop behövs. Vi går igenom säkerhet,
                får våra platser vid skarvborden och skriver under *Laser i verkstaden*.

                Mikael
                """,
                Se(2025, 10, 3, 14, 0),
                "mikael",
                true,
                true,
                [
                    new("#2", "Får man ha kontaktlinser under skyddsglasögonen?", 3),
                    new("mikael", "Ja, det går bra. Skyddsglasögonen tar hand om resten.", 5),
                    new("#7", "Jag har egna arbetsskor, men de är stålhätta på bara ena sidan 😅", 7),
                    new("mikael", "Ha, då får du köpa ett par till. Vi har lånepar om du kommer sent.", 9),
                    new("#11", "Måndag 8.30 inne i verkstaden eller utanför?", 22),
                    new("mikael", "Inne. Dörren är öppen från 8.15.", 23)
                ]),
            new(
                "Skarvskydd: köp 60 mm",
                """
                Till skarvprovet behöver varje elev egna skarvskydd. Vi köper in en förpackning till alla, men
                säg till om du vill ha fler för att öva hemma. Standardstorleken i vår skarvkassett är **60 mm**.
                """,
                Se(2025, 11, 5, 10, 30),
                "mikael",
                false,
                true,
                [
                    new("#4", "Är det 40 eller 60 mm vi ska ha? Står 40 på kassetten jag fått.", 2),
                    new(
                        "mikael",
                        "Bra att du frågar. Kassetterna på bord 1–4 tar 40 "
                        + "mm, resten 60. Kolla ditt bord.",
                        3),
                    new("#4", "Bord 3 alltså. Då tar jag 40 🙂", 4),
                    new("#9", "Går det att köra 60 i en 40-kassett om man lägger det lite snett?", 6),
                    new("mikael", "Nej. Det blir en snäv böj och dämpningen ökar. Du blir underkänd på det.", 7),
                    new("#9", "Förstått. Fråga var väl på sin plats.", 8)
                ]),
            new(
                "Skarvprov: lista för bokning",
                """
                Skarvproven genomförs vecka 48. Boka en tid på listan som sitter på dörren till verkstaden.
                Varje prov tar cirka 90 minuter och vi tar högst sex personer per dag. Kom tio minuter innan
                och tänk på att äta frukost.
                """,
                Se(2025, 11, 17, 9, 15),
                "mikael",
                false,
                true,
                [
                    new("#1", "Går det att byta tid om man blir sjuk?", 4),
                    new("mikael", "Ja, ring före 8 samma dag så flyttar vi.", 5),
                    new("#14", "Kan jag skriva upp mig onsdag em? Listan är full torsdag.", 28),
                    new("camilla", "Jag har lagt dig på onsdag kl. 13.", 30),
                    new("#14", "Tack Camilla!", 31)
                ]),
            new(
                "Så gick skarvproven",
                """
                Nu är alla skarvprov genomförda och rättade. Det gick väldigt bra: alla klarade kravet på under
                0,1 dB, och flera hade ett snitt under 0,05. Återkoppling finns på varje inlämning. Nästa vecka
                börjar vi med OTDR, och då ses vi i mätrummet.

                Bra jobbat, allihop!
                """,
                Se(2025, 11, 28, 15, 40),
                "mikael",
                false,
                true,
                [
                    new("#6", "Tack för snabb rättning!", 2),
                    new("#12", "Min kassett var fortfarande varm när jag lade den. Jag sa inget 😬", 5),
                    new("mikael", "Jag såg det. Men det var ju rätt gjort att vänta lite till.", 6)
                ]),
            new(
                "OTDR-bokning vecka 49",
                """
                OTDR:n i mätrummet är bokningsbar i pass om 45 minuter. Skriv upp dig på listan som hänger
                utanför rummet. Ta med skyddsglasögon och er kurvmapp. Vi mäter en länk på tre kilometer, mellan
                verkstaden och anslutningsboxen i korridoren.
                """,
                Se(2025, 12, 2, 8, 20),
                "mikael",
                false,
                true,
                [
                    new("#3", "Kan vi vara tre per pass eller är det max två?", 3),
                    new("mikael", "Max tre. Två mäter, en skriver protokoll, och sen byter ni.", 4),
                    new("#8", "Vi är fyra i vår grupp. Kan vi dela upp oss på två pass?", 6),
                    new("mikael", "Ja, gör det.", 7)
                ])
        ]);

    private static CourseSpec FiberPon() => new(
        "FIB25_PON",
        "Passiva fibernät och installation",
        "Passiva optiska nät (PON): uppbyggnad, installation hos kund och dokumentation. Kursen avslutas med ett "
        + "praktiskt installationsprov.\n\n"
        + "**Efter kursen kan du**\n\n"
        + "1. förklara hur ett PON-nät är uppbyggt och räkna en dämpningsbudget\n"
        + "2. installera och ta en anläggning i drift hos kund\n"
        + "3. mäta, dokumentera och lämna över en färdig anläggning",
        35,
        new DateOnly(2026, 10, 5),
        new DateOnly(2026, 11, 27),
        "roger",
        [
            Week(
                "Vecka 1 – PON-nätets uppbyggnad och dämpningsbudget",
                "OLT, splitter, ONT och hur man räknar på ett nät.",
                new DateOnly(2026, 10, 5),
                [
                    Lesson(
                        "Teori: Från central till kund: OLT, splitter, ONT",
                        """
                        PON betyder Passive Optical Network, ett nät där en enda fiber delas av många kunder med
                        hjälp av passiva splittrar, alltså komponenter utan egen ström. Det gör nätet billigt att
                        bygga och driva: det finns ingen aktiv utrustning ute i fält, bara glas och plast.

                        Kedjan ser ut så här. I stationen sitter **OLT** (Optical Line Terminal), som skickar och tar
                        emot ljus. Från OLT går en matarfiber ut i nätet, ofta flera kilometer, till en eller flera
                        **splittrar** som delar ljuset på 2, 4, 8, 16 eller 32 grenar. Hos kunden slutar fibern i
                        en **ONT** (Optical Network Terminal) som omvandlar ljuset till ethernet för routern.

                        Nedströms, alltså från OLT till kunderna, skickas allt på 1490 nm och når alla kunder, som
                        sedan filtrerar ut det som är deras. Uppströms, från kund till OLT, används 1310 nm och
                        kunderna får turas om, vilket styrs av OLT:n med tidsluckor. Därför kan du inte "mäta
                        tvärs" mellan två kunder: allt går via OLT.

                        Att man delar på ljuset kostar. Varje gång ljuset delas på två förlorar varje gren
                        ungefär 3,5 dB, så en 1:8-splitter kostar cirka 10,5 dB och en 1:32 cirka 17,5 dB. De siffrorna
                        är det som sätter gränsen för hur långt och hur många kunder ett nät kan ha. Standarden
                        (G-PON) har klasser, där **klass B+** tillåter upp till 28 dB dämpning mellan OLT och ONT.

                        ## PON i praktiken

                        Alla delar av nätet ska märkas och dokumenteras eftersom det inte syns något. Är du på plats
                        och ser en fiber i en skarvbox vill du veta vart den går, och vilken splitter den hör till.
                        Därför är märkning och ritningar halva jobbet, och det är därför en dokumentation som är
                        slarvig kostar mer än en dåligt skarvad fiber.
                        """,
                        "Från central till kund: OLT, splitter, ONT"),
                    Lesson(
                        "Teori: Dämpningsbudget",
                        """
                        En dämpningsbudget är en enkel räkning som svarar på frågan: *kommer signalen fram?*
                        Du summerar all dämpning från OLT till ONT och jämför med vad utrustningen tål.

                        Posterna är alltid desamma:

                        - **Fibern**: längd i km gånger dämpningskoefficienten, vanligen 0,35 dB/km vid 1310 nm
                        - **Skarvar**: antal gånger 0,1 dB (gränsvärde, i praktiken oftast 0,05)
                        - **Kontakter**: antal gånger 0,5 dB (gränsvärde), där varje kontaktpar räknas som en
                        - **Splitter**: 3,5 dB per delning (1:2), 7,2 dB för 1:4, 10,5 dB för 1:8 och så vidare
                        - **Reserv**: minst 3 dB som marginal för framtida skarvar, reparationer och åldrande

                        Summan jämförs med klassen. För B+ får totalen inte överstiga 28 dB, och det räcker inte
                        att komma strax under: räkna alltid med reserven. En budget som landar på 27,5 dB är i
                        praktiken fel eftersom ni inte har någon marginal alls.

                        Ett exempel. Matarfiber på 6 km vid 1310 nm ger 2,1 dB. Tio skarvar på 0,1 dB ger 1 dB.
                        Fyra kontaktpar på 0,5 dB ger 2 dB. En tvåstegs splitter, 1:4 följd av 1:8, ger 7,2 + 10,5 =
                        17,7 dB. Summa 22,8 dB. Med 3 dB reserv blir det 25,8 dB, vilket ryms inom B+ med
                        2,2 dB kvar.

                        ## Dämpningsbudget i praktiken

                        Det är ofta budgeten som avgör var man placerar splittern. Placerar du den långt från OLT får
                        du lång matarfiber men kort fördelningsfiber, och tvärtom. Det finns ingen "rätt" lösning, men
                        du måste kunna visa hur du räknat. Det är det som bedöms i dimensioneringsuppgiften.
                        """,
                        "Dämpningsbudget"),
                    Exercise(
                        "Övningsuppgift: Räkna en dämpningsbudget",
                        """
                        För att öva på att räkna ska vi utgå från följande nät. En OLT i stationen matar 64
                        hushåll via en tvåstegs splitter (1:4 i stolpskåpet, 1:16 i brunnen vid gatan). Längsta
                        avståndet från OLT till en ONT är 7,5 km. Vi mäter vid 1310 nm.

                        Antag följande:

                        - fiber: 0,35 dB/km
                        - skarvar: 9 stycken, 0,1 dB vardera
                        - kontakter: 5 kontaktpar, 0,5 dB vardera
                        - splitter 1:4: 7,2 dB och splitter 1:16: 13,8 dB
                        - reserv: 3 dB

                        Räkna ut:

                        - dämpningen för varje post, och total dämpning utan reserv
                        - total dämpning med reserv, och om den ryms inom klass B+ (28 dB)
                        - hur lång matarfibern får vara innan budgeten tar slut. Visa hur du räknade

                        Räkna för hand först, och kontrollera sedan i ett kalkylblad. Skriv gärna ned i vilken
                        ordning du räknar, så att du kan jämföra med en kamrat. Du får lämna in svaret som text om
                        du vill ha kommentarer.
                        """,
                        _text),
                    Assignment(
                        "Inlämning: Dimensionera ett PON-nät",
                        """
                        Rita ett PON-nät för **48 hushåll** och räkna ut dämpningsbudgeten för det längsta
                        avståndet. Du väljer själv splitterstruktur (en- eller tvåsteg), var splittrarna ska sitta
                        och hur långt det är till de olika grupperna. Redovisa dina antaganden.

                        Inlämningen ska innehålla:

                        - en enkel skiss av nätet (för hand eller i ett ritprogram, fotograferad skiss duger)
                        - en tabell med dämpningsbudgeten för den längsta vägen, med varje post för sig
                        - en rad om hur mycket reserv du har kvar och i vilken klass nätet ryms
                        - kort motivering av din splitterstruktur (varför just den)

                        ==Visa hur du räknat==, inte bara slutsiffran. Antaganden om dämpning för fiber, skarvar,
                        kontakter och splittrar tar du från teorin.

                        💡 Tips: Det är lätt att komma undan med för tajta marginaler. Om din budget landar nära
                        28 dB har du i praktiken ingen reserv, och då är det rätt att bygga om nätet. Det är
                        ingen idé att vara snål med en splitter.

                        ## Att lämna in

                        - [ ] Skiss över nätet
                        - [ ] Tabell med dämpningsbudget för längsta vägen
                        - [ ] Motivering av splitterstruktur och reserv

                        **Betygskriterier**

                        Uppgiften visar kursmål 1: att förklara hur ett PON-nät är uppbyggt och räkna en
                        dämpningsbudget. **G**: korrekt uppställd budget, med rimliga antaganden och en klass.
                        **VG**: dessutom en jämförelse av minst två alternativa uppbyggnader och en motivering av valet.
                        """,
                        Friday(new DateOnly(2026, 10, 5), 2),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.MostlyGraded,
                        [
                            "Nätet har en OLT, en 1:8-splitter i stolpskåpet och 1:8 vid gatan. Längsta avstånd är "
                            + "4,2 km. Dämpningsbudget: fiber 1,5 dB, 6 skarvar 0,3 dB, 4 kontakter 1,6 dB, "
                            + "splitter 21 dB. Summa 24,4 dB, vilket ryms inom klass B+.",
                            "Tvåsteg: 1:4 i stationen och 1:12 vid gatan, 48 hushåll. Längsta väg 5,8 km. Fiber 2,0 "
                            + "dB, 8 skarvar 0,8 dB, 4 kontakter 2,0 dB, splitter 7,2 + 11,0 = 18,2 dB. Summa 23,0 "
                            + "dB, med 3 dB reserv 26,0 dB. Ryms inom B+ med 2 dB marginal. Jag valde två steg för "
                            + "att spara fibrer i matarkabeln.",
                            "Jag jämförde två lösningar. A: enkel 1:32-splitter i stolpskåpet, 17,5 dB, total "
                            + "22,6 dB. B: 1:4 + 1:8, 17,7 dB, total 22,8 dB. Skillnaden är liten, men B ger fler "
                            + "reservfibrer. Jag valde B.",
                            "Nät för 48 hushåll, längsta väg 3 km. 1:8 + 1:8 = 21 dB, fiber 1,05 dB, skarvar 0,5 "
                            + "dB, kontakter 1,5 dB. Summa 24,05 dB. Reserv: 3,95 dB till 28."
                        ],
                        [
                            "Tydlig budget och rimliga antaganden. Godkänd.",
                            "Bra att du räknade med reserv och visade alla poster. VG: jämförelsen av två "
                            + "uppbyggnader lyfter arbetet.",
                            "Godkänd. Skriv ut hur du kommit fram till antalet skarvar nästa gång."
                        ],
                        [
                            "Budgeten saknar kontakterna vid ONT och OLT. Lägg till dem, räkna om och lämna in igen.",
                            "Du har räknat splittern som 3,5 dB oavsett steg. En 1:8 är cirka 10,5 dB. Rätta och "
                            + "lämna in igen."
                        ],
                        "Dimensionera ett PON-nät")
                ],
                "Uppbyggnad av PON-nät"),
            Week(
                "Vecka 3 – Installation hos kund och idrifttagning",
                "Dragning, uttag och idrifttagning.",
                new DateOnly(2026, 10, 19),
                [
                    Lesson(
                        "Teori: Installation steg för steg",
                        """
                        En installation hos kund börjar med en titt på platsen innan du tar fram något verktyg.
                        Var kommer fibern in i huset? Var ska uttaget sitta? Hur går kabelvägen utan att göra
                        skada på kundens inredning? Ta gärna ett foto före, så har du dokumentation om något
                        skulle ifrågasättas senare.

                        Kabelvägen planeras med minsta böjradie i åtanke. En fiberkabel tål inte att knäckas: för
                        en vanlig kabel är minsta böjradie cirka 10 gånger kabelns ytterdiameter under drift, och
                        20 gånger vid dragning. För böjokänslig fiber (G.657) är det lite bättre, men inte oändligt.
                        Fäst kabeln med lämpliga clips, inte med häftpistol, och undvik att dra nära starkström.

                        Vid ytterväggen går kabeln in genom en tätad genomföring. Hålet ska vara rent, lutande utåt
                        så vatten rinner bort, och tätas med brandtätning och fogmassa. Inne i huset avslutas
                        kabeln i ett fiberuttag eller en liten box, där fibern skarvas till en kontaktpigtail och
                        läggs i en kassett med lite slack (reservlängd), cirka 1–2 meter.

                        Idrifttagning innebär att man kontrollerar nivån med en effektmätare: mottagen effekt vid
                        ONT ska ligga inom intervallet i databladet, ofta –8 till –27 dBm. Därefter kopplas ONT
                        och man kontrollerar att den får kontakt med nätet (ljus på rätt lampa), att kunden får
                        en IP-adress och att anslutningen fungerar.

                        ## Installation i praktiken

                        Det är ofta det sista tio minuterna som avgör hur kunden upplever dig. Visa vad du gjort,
                        förklara hur routern ska stå, och be kunden kvittera. Och lämna ingenting efter dig, inte
                        ens en liten bit fiberskräp.
                        """,
                        "Installation steg för steg"),
                    Lesson(
                        "Teori: Kundbemötande och arbetsmiljö",
                        """
                        När du är hemma hos en kund representerar du företaget, och många kunder har aldrig haft en
                        tekniker hemma förut. Presentera dig, visa legitimation och berätta kort vad du ska göra och
                        hur länge du beräknar att det tar. Fråga innan du flyttar möbler, och lägg golvskydd där
                        du går.

                        Förklara på enkelt språk. Ingen kund vill höra om dB och OLT. De vill veta om internet
                        fungerar, var routern ska stå och vad de ska göra om det slutar fungera. Om du har gjort
                        något annorlunda än kunden förväntat sig, till exempel dragit kabeln en annan väg, säg det
                        direkt.

                        Arbetsmiljön på plats är din egen: stege, borrmaskin och kemikalier ska hanteras som i
                        verkstaden. Ställ stegen stadigt, använd skyddsglasögon vid borrning och sopa upp efter dig.
                        Är arbetet farligt, till exempel för att taket är halt, kan du avstå. Det är aldrig
                        fel att ringa arbetsledaren.

                        Dokumentera under tiden. Foto av kabelväg, uttag och mätvärde tar en minut, och sparar mer
                        än den tiden om kunden ringer om en vecka.

                        ## Bemötande i praktiken

                        Under fältdagen spelar lärarna kund. Räkna med frågor som "Måste jag ha bort mattan?" och "Kan
                        du inte bara dra kabeln längs listen?". Svara hövligt, tydligt och sanningsenligt. Ni
                        bedöms på det också.
                        """,
                        "Kundbemötande och arbetsmiljö"),
                    Assignment(
                        "Inlämning: Dokumentation av färdig anläggning",
                        """
                        Dokumentera den installation du gjorde under fältövningen: kabelväg, märkning, mätvärden
                        och foton. Skriv så att en kollega som aldrig varit på platsen kan hitta alla delar.

                        Dokumentationen ska innehålla:

                        - en skiss eller beskrivning av kabelvägen, från anslutningspunkt till uttag
                        - märkning av kabel, uttag och fiber, så som den sitter på plats
                        - mätvärden: mottagen effekt vid uttaget, och OTDR-värden om du mätt
                        - minst **fyra foton** med en beskrivning av vad varje bild visar (vi kan inte se
                        bilderna i texten, så skriv vad de föreställer och varför de är med)
                        - eventuella avvikelser från planen, och vad du gjorde åt dem

                        💡 Tips: Fotona behöver inte vara snygga, men de ska visa det som är viktigt. Ett bra
                        foto visar en märkning i närbild, ett annat kabelvägen i helhet.

                        ## Att lämna in

                        - [ ] Beskrivning av kabelväg och märkning
                        - [ ] Mätvärden från uttaget
                        - [ ] Minst fyra fotobeskrivningar och eventuella avvikelser

                        **Betygskriterier**

                        Uppgiften visar kursmål 3: att mäta, dokumentera och lämna över en färdig anläggning. För
                        godkänt ska dokumentationen vara fullständig och så tydlig att en kollega kan hitta alla delar.
                        """,
                        Se(2026, 10, 30, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Early,
                        null,
                        null,
                        null,
                        "Dokumentation av färdig anläggning"),
                    Assignment(
                        "Praktik: Installationsprov hos kund",
                        """
                        Provet genomförs på övningsanläggningen i Kungsängen torsdag 5 november. Du installerar ett
                        uttag och tar anläggningen i drift inför läraren. Du får **tolv minuter per moment**, och
                        läraren bedömer medan du arbetar.

                        Det här kontrolleras på plats:

                        - [ ] Legitimation, skyddsglasögon och arbetskläder med dig
                        - [ ] Presentation av dig själv och av vad du ska göra, för "kunden" (läraren)
                        - [ ] Golvskydd utlagt och kundens utrymme lämnat rent efteråt
                        - [ ] Kabelvägen följer minsta böjradie och är väl fäst
                        - [ ] Uttaget är monterat, fibern skarvad och kassetten ordnad med rätt slack
                        - [ ] Märkningen stämmer med dokumentationen
                        - [ ] Mottagen effekt uppmätt och inom gränsvärdet, antecknad i protokollet
                        - [ ] Anläggningen är driftsatt: ONT har kontakt och du visar kunden att den fungerar

                        ==Säkerheten bedöms hela tiden==: skyddsglasögon, smulsburk och säker stege. Gör du något
                        farligt avbryter läraren, och det kan innebära att provet görs om.

                        💡 Tips: Säg högt vad du gör. Det är lättare för oss att se att du kan det, och du hittar
                        själv missar när du säger dem.

                        **Betygskriterier**

                        Provet visar kursmål 2: att installera och ta en anläggning i drift hos kund. **G**:
                        samtliga punkter ovan, utan allvarliga säkerhetsbrister. **VG**: dessutom ett tydligt och
                        tryggt kundbemötande, och att du klarar momenten inom tiden utan att behöva hjälp.
                        """,
                        Se(2026, 11, 5, 15, 0),
                        GradeScale.PassFailDistinction,
                        _onSite,
                        SubmissionPattern.None,
                        null,
                        null,
                        null,
                        "Installationsprov hos kund")
                ],
                "Installation hos kund"),
            Week(
                "Vecka 5 – Dokumentation, slutmätning och överlämning",
                "Mätprotokoll och överlämning av en färdig anläggning.",
                new DateOnly(2026, 11, 2),
                [
                    Lesson(
                        "Teori: Mätprotokoll och överlämning",
                        """
                        En anläggning är inte klar förrän den är dokumenterad. Det låter som ett byråkratiskt krav, men
                        det är det som avgör om nästa tekniker, kanske du själv om ett år, hittar felet på tio
                        minuter eller på tre timmar. Ett bra protokoll är kort, tydligt och fullständigt.

                        Slutmätningen görs i två steg. Först en **effektmätning** med ljuskälla och effektmätare,
                        som visar total dämpning i länken. Den mäts vid 1310 och 1550 nm. Därefter en
                        **OTDR-mätning** som visar var dämpningen sitter, så att du kan hitta en dålig skarv eller
                        kontakt. Mätvärdena jämförs med dämpningsbudgeten. Ligger de över är det dags att felsöka:
                        rengör kontakterna, mät om och titta på kurvan.

                        Protokollet ska innehålla anläggningens namn och adress, datum, vem som mätte, instrumentets
                        serienummer och kalibreringsdatum, mätvärdena per fiber, avvikelser med åtgärd och en
                        underskrift. Kurvfilerna sparas och bifogas. Foton på märkning och kabelväg hör också
                        hit.

                        Överlämningen innebär att du visar beställaren eller nätägaren anläggningen, går igenom
                        dokumentationen och får den godkänd. Hos en privatkund är det oftast ett kvitto. Hos en
                        nätägare kan det vara en längre besiktning med checklista.

                        ## Överlämning i praktiken

                        Ett vanligt fel är att protokollet skrivs i efterhand, på minnet, kvällen innan. Skriv det
                        medan du mäter. Det går fortare, och siffrorna blir rätt.
                        """,
                        "Mätprotokoll och överlämning"),
                    Assignment(
                        "Inlämning: Slutmätning och mätprotokoll",
                        """
                        Mät den installerade anläggningen med OTDR och effektmätare och lämna in protokollet.
                        Du mäter från samma anslutningspunkt som under installationsprovet, vid både 1310 och 1550
                        nm, och jämför med din dämpningsbudget.

                        Protokollet ska innehålla:

                        - anläggningens namn, datum och vem som mätte
                        - instrument, serienummer och inställningar (pulsbredd, mätområde)
                        - total dämpning från effektmätaren, och dämpning per händelse från OTDR
                        - en jämförelse med dämpningsbudgeten: ryms värdena inom gränsvärdena?
                        - avvikelser och åtgärder

                        ==Om något ligger över gränsvärdet== ska du felsöka, åtgärda och mäta om, och redovisa
                        både före och efter.

                        ## Att lämna in

                        - [ ] Ifyllt mätprotokoll
                        - [ ] Jämförelse mot dämpningsbudget
                        - [ ] Kurvfilens namn och eventuella avvikelser

                        **Betygskriterier**

                        Uppgiften visar kursmål 3: att mäta, dokumentera och lämna över en färdig anläggning. För
                        godkänt ska protokollet vara fullständigt, värdena korrekta och avvikelser redovisade.
                        """,
                        Se(2026, 11, 13, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.None,
                        null,
                        null,
                        null,
                        "Slutmätning och mätprotokoll")
                ],
                "Dokumentation och slutmätning")
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
                [
                    new("#10", "Måste man ha stålhätta eller räcker det med vanliga arbetsskor?", 4),
                    new("roger", "Stålhätta ska det vara. Har du inga får du låna.", 5),
                    new("#2", "Finns det en checklista för vad som bedöms?", 8),
                    new("roger", "Ja, den ligger i provet här i kursen. Läs den innan torsdag.", 9),
                    new("#2", "Hittade den, tack!", 10)
                ]),
            new(
                "Resultat på PON-dimensioneringen",
                """
                Jag har nu rättat de flesta av era PON-dimensioneringar. De flesta är bra, men en vanlig miss
                är att man glömmer kontakterna vid OLT och ONT i budgeten. Räkna alltid med dem! Läs
                kommentarerna på er inlämning. De som ska komplettera har fått besked.
                """,
                Se(2026, 10, 22, 16, 30),
                "roger",
                false,
                true,
                [
                    new("#5", "Räknar man kontakterna vid OLT som två eller en?", 3),
                    new(
                        "roger",
                        "Ett kontaktpar är en kontakt i budgeten. OLT-sidan "
                        + "är ett par, ONT-sidan ett till.",
                        4),
                    new("#5", "Aha, då hade jag tagit för många. Tack!", 5),
                    new("#13", "Får man komplettera efter fredag?", 24),
                    new("roger", "Ja, men senast 6 november.", 26)
                ]),
            new(
                "Skarvskydd och verktyg till fältdagen",
                """
                Till fältdagen packar jag verktygslådor med skarvaren, klyvverktyg, IPA och effektmätare. Ni
                behöver bara ta med era egna skyddsglasögon, arbetskläder, legitimation och en penna. Är det
                någon som har egna skarvskydd som den vill använda? Säg till så kontrollerar vi att de passar.
                """,
                Se(2026, 10, 29, 10, 10),
                "roger",
                false,
                true,
                [
                    new("#8", "Vilken storlek är det på skarvskydden vi har i lådan, 40 eller 60?", 2),
                    new("roger", "60 mm. Lådan på övningsanläggningen är samma som verkstadens bord 5–8.", 3),
                    new("#1", "Vi hade 40 i skarvkassetten förra veckan...", 4),
                    new("roger", "Bra pekat, jag kollar innan torsdag. Jag tar med båda storlekarna.", 5)
                ]),
            new(
                "Fältdagen: bussen går 7.30",
                """
                Torsdag 5 november åker vi från verkstaden **07.30** med minibuss till Kungsängen. Vi är tillbaka
                cirka 16.30. Ta med matsäck och varma kläder, för vi arbetar delvis utomhus. Det blir inget
                kaffe på plats, men det finns en termos i bussen.

                Mikael och jag kör.
                """,
                Se(2026, 11, 2, 7, 45),
                "roger",
                false,
                true,
                [
                    new("#4", "Kan man åka med bussen hem direkt efter sitt prov?", 2),
                    new("roger", "Nej, vi åker tillsammans. Ta med något att göra.", 3),
                    new("#9", "Får man ha hörlurar på i bussen? 😄", 4),
                    new("roger", "Ja, bara inte vid provet.", 5)
                ])
        ]);

    private static CourseSpec FiberLia() => new(
        "FIB25_LIA1",
        "Fältpraktik (LIA) 1",
        "Första perioden lärande i arbete som fibertekniker hos ett installationsföretag.\n\n"
        + "**Efter kursen kan du**\n\n"
        + "1. arbeta som fibertekniker under handledning hos ett installationsföretag\n"
        + "2. dokumentera ditt arbete och reflektera över det\n"
        + "3. följa säkerhetsregler och arbetsrutiner på en riktig arbetsplats",
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
                        "Teori: Så hittar du en praktikplats",
                        """
                        Du hittar din praktikplats genom att själv ta kontakt med installatörer och nätägare i
                        regionen. Det är det som är övningen: de flesta av er får sitt första jobb på samma sätt.
                        Vi hjälper till med kontakter, men det är du som söker.

                        Börja med en lista på tio företag. Titta på hemsidor och lediga tjänster, fråga i klassen,
                        prata med dem ni mött under fältdagarna och ring. Ett samtal fungerar ofta bättre än ett
                        mejl. Säg vem du är, vad du läser, när du kan börja och att du söker en plats för
                        **lärande i arbete** på 16 veckor. Ha en fråga klar: vad gör ni, och vad skulle jag kunna
                        göra hos er?

                        Ta med intyg om genomförd säkerhetsutbildning och ditt CV. Företagen frågar nästan alltid
                        om körkort (B) och om du kan vara ute på fältet i kyla. Tänk på det i förväg. Många
                        företag vill också ha ett kort prov eller en provdag innan de bestämmer sig.

                        När ni har kommit överens ska företaget utse en **handledare** som du jobbar nära. Handledaren
                        skriver under ett avtal tillsammans med skolan och dig, och det är handledaren som
                        bedömer dina arbetsuppgifter.

                        ## Så söker du i praktiken

                        Skriv ner varje kontakt: datum, vem du pratat med och vad ni kom överens om. Det ser ut
                        som en omväg, men det är det som gör att du inte ringer samma företag två gånger. Berätta för
                        Sofie så fort du har en plats, eller om det kärvar. Det gör ingenting om du inte har hittat
                        något i november, men vi vill veta att du söker.
                        """,
                        "Så hittar du en praktikplats"),
                    Lesson(
                        "Teori: Handledare, praktikdagbok och säkerhet på plats",
                        """
                        Din handledare är den som lär dig yrket under praktiken, och det är normalt en erfaren
                        tekniker. Räkna med att hen är upptagen, så ställ frågor när det passar och skriv upp
                        sådant du undrar över till rasten. Det ger bättre svar än att avbryta mitt i ett moment.

                        Du för en **praktikdagbok**, några rader varje dag: vad du gjort, vad du lärt dig och
                        vad som var svårt. Dagboken är underlag för samtalen med Sofie och för
                        bedömningen, och den hjälper dig att komma ihåg vad du faktiskt kan när du ska söka jobb.

                        Säkerhetsreglerna på en riktig arbetsplats är samma som i verkstaden, men miljön är
                        stökigare. Stå aldrig under något lyft, ta alltid med skyddsglasögon, och ring innan du
                        går ner i en brunn eller upp på en stolpe. Är du osäker på ett moment ska du säga det. Det är
                        aldrig fel att fråga, och ingen ska låta dig göra något du inte fått lära dig.

                        Sjukanmälan går till företaget och till skolan samma morgon. Kommer du för sent ringer
                        du handledaren. Små saker, men det är sådant som avgör om du får ett erbjudande efteråt.

                        ## Dagboken i praktiken

                        Skriv i telefonen på bussen hem om det är det som fungerar. Tre rader som är sanna är bättre än
                        en sida som är påhittad, och vi läser inte efter stavfel.
                        """),
                    Exercise(
                        "Övningsuppgift: Presentera dig för en installatör",
                        """
                        Innan du ringer ett företag är det bra att ha sagt det högt några gånger. Skriv ett
                        manus på högst tio meningar för ett första samtal med en installatör och öva sedan på det
                        med en kamrat som spelar chef.

                        Manuset ska innehålla:

                        - vem du är och vilken utbildning du går
                        - vilka moment du har gjort i verkstaden (skarvning, OTDR, installation)
                        - vad du söker (16 veckors LIA från 18 januari)
                        - en fråga om företaget

                        Byt roller och ge varandra två saker som var bra och en sak att ändra. Ni behöver inte
                        lämna in något, men ta med manuset till nästa träff med Sofie.
                        """,
                        _text),
                    Assignment(
                        "Inlämning: Anmäl din praktikplats",
                        """
                        Anmäl din praktikplats här så snart du har kommit överens med ett företag. Ange:

                        - företagets namn och ort
                        - handledarens namn, e-post och telefonnummer
                        - startdatum (utbildningens LIA börjar 18 januari) och arbetstider
                        - vilken typ av arbete ni har pratat om (fiberinstallation, nätbygge, felsökning)

                        Har du ännu inte hittat någon plats ska du skriva vilka företag du kontaktat och vad de
                        svarat, så vi kan hjälpa till. Vi godkänner platsen när vi pratat med handledaren.

                        ## Att lämna in

                        - [ ] Företag, handledare och kontaktuppgifter
                        - [ ] Startdatum och arbetstider
                        - [ ] Kort beskrivning av arbetsuppgifterna

                        **Betygskriterier**

                        Uppgiften visar kursmål 1: att arbeta som fibertekniker under handledning hos ett
                        installationsföretag. För godkänt ska uppgifterna vara kompletta och platsen ge möjlighet
                        att arbeta praktiskt med fiber.
                        """,
                        Se(2026, 11, 27, 23, 59),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.None,
                        null,
                        null,
                        null,
                        "Anmäl din praktikplats")
                ])
        ],
        [
            new(
                "LIA 1: så här går det till",
                """
                Hej! Nu är det dags att börja tänka på LIA. Kort om upplägget: 16 veckor hos ett företag, start 18
                januari, med en handledare på plats. Jag besöker er en gång under perioden och ni skriver
                praktikdagbok varje dag. I kursen finns en anmälan där ni anger plats och handledare. Frågor?

                Sofie
                """,
                Se(2026, 10, 20, 10, 0),
                "sofie",
                true,
                true,
                [
                    new("#3", "Får man göra LIA hos två olika företag under perioden?", 3),
                    new("sofie", "Ja, om båda företagen har en handledare och det går att få ihop till 16 veckor.", 5),
                    new("#6", "Betalt eller obetalt?", 8),
                    new("sofie", "Obetalt, tyvärr. Men flera företag brukar erbjuda sommarjobb efteråt.", 10),
                    new("#6", "Okej, tack för ärligt svar.", 11)
                ]),
            new(
                "Företagsbesök: Stolpen Fiber berättar om jobbet",
                """
                Torsdag 12 november kommer en tekniker från ett lokalt fibernätsföretag och berättar om hur det är att
                jobba som fibertekniker, vad de söker hos praktikanter och hur en vanlig dag ser ut. Kl. 13.00 i
                klassrummet. Ta med frågor, de brukar vilja ha dem.
                """,
                Se(2026, 10, 28, 9, 0),
                "sofie",
                false,
                true,
                [
                    new("#2", "Kommer de ta emot praktikanter i vår?", 4),
                    new("sofie", "Det ska du få fråga dem själv. De sa att de tar emot två.", 6),
                    new("#12", "Kan man ta med CV?", 7),
                    new("sofie", "Absolut.", 8)
                ]),
            new(
                "Påminnelse: anmäl plats senast 27 november",
                """
                En kort påminnelse: anmälan av praktikplats ska vara inne senast **fredag 27 november**. Har du
                inte hittat någon plats ännu, hör av dig till mig i god tid så hjälper jag dig vidare. Det är
                ingen skam, men det är bra att veta om det i förväg.
                """,
                Se(2026, 11, 2, 9, 30),
                "sofie",
                false,
                true,
                [
                    new("#10", "Räcker det med en muntlig överenskommelse eller ska man ha något skriftligt?", 3),
                    new("sofie", "Muntligt räcker för anmälan, avtalet skriver vi tillsammans sen.", 4)
                ])
        ]);
}
