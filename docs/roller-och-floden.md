# Roller och flöden

Admin bestämmer vem som undervisar vad, och läraren bestämmer vad som händer i kursen. Eleven läser och lämnar in.

Modellen beslutades 30 oktober 2026 och byggs i #269. Per kurs-lärare är #274, klassidans flöde är #275 och separationen av admin är #276.

## Roller

Den globala rollen säger vad du administrerar. Din koppling till en kurs eller klass säger vad du gör där.

|                                          | Admin                         | Lärare, egna kurser | Elev               |
| ---------------------------------------- | ----------------------------- | ------------------- | ------------------ |
| Skola, användare, integrationer          | Hanterar                      | –                   | –                  |
| Klasser, inskrivning, lärare på kurs     | Hanterar                      | Läser klasslistan   | Egna klasskamrater |
| Skapa och ta bort kurser                 | Hanterar                      | –                   | –                  |
| Kursens titel, poäng, datum, beskrivning | Hanterar (utom beskrivningen) | Hanterar            | Läser              |
| Veckor, innehåll, nyheter                | Läser                         | Hanterar            | Läser publicerat   |
| Inlämningar och betyg                    | –                             | Hanterar            | Bara egna          |
| Lämna in, kommentera, klarmarkera        | –                             | –                   | Ja                 |

**Vem ändrar vad på en kurs.** En lärare som är tilldelad kursen får ändra kursens titel, poäng, datum och beskrivning, och bygger veckorna och innehållet. Admin skapar och tar bort kurser och tilldelar lärarna. Det finns ingen arkivering: en kurs är avslutad när slutdatumet har passerat, och en klass när alla dess kurser är det. Beskrivningen är lärarens egen text, så admin skriver den inte.

Admin har ingen genväg in i undervisningen. Är en lärare sjuk tilldelar admin en vikarie, eller sig själv om hen också undervisar. Betyget visar alltid vem som formellt var lärare.

Eleven ser namn på dem i samma grupp, och kontaktuppgifter till kursens lärare. Aldrig andras inlämningar, betyg eller framsteg.

## Skolans år

Sidorna byggs efter arbetet, inte efter databasens objekt.

1. **Sätt upp klassen (admin, en gång).** Skapa klassen, lägg in eleverna.
2. **Planera terminen (admin, några gånger om året).** Skapa varje kurs från klassens sida, med datum och lärare. Har kursen gått förut utgår admin från den tidigare kursen, så följer namn, omfattning, lärare och valda veckor med.
3. **Förbered kursen (lärare).** Kursen syns i Idag under Behöver förberedas när beskrivningen är tom, eller när kursen inte har börjat och saknar publicerade delar. Läraren skriver beskrivningen, bygger veckorna eller kopierar dem från ett tidigare tillfälle och publicerar.
   En ny, tom vecka är också ett utkast och syns inte för eleverna förrän den har en publicerad del. Allt kopierat är utkast. En vecka med bara utkast syns inte för eleverna, oavsett datum, tills läraren väljer Publicera veckan eller Publicera alla veckor. Delarna syns sedan på sina egna datum.
4. **Kör kursen (lärare och elev, varje vecka).** Läraren publicerar och bedömer, eleven läser och lämnar in. Här ligger nästan all tid i appen.
5. **Stäng och upprepa (lärare, sedan admin).** Läraren bedömer klart, kursen tar slut och nästa tillfälle utgår från det gamla.

## Var saker hör hemma

- **Klassen** är en årskull och håller eleverna. Den har ingen beskrivning.
- **Kursen** håller lärarna, veckorna och innehållet. Varje rad är ett tillfälle med egna datum, så de flesta kurser hör till en klass.
- **Klassens sida** är admins arbetsyta. Ny kurs är den primära handlingen där och finns bara på en klass eller grupp, inte på Alla grupper: först väljer admin en tidigare kurs eller en tom kurs, sedan namn, kod, omfattning, datum och lärare, och kursen skapas, kopplas till klassen och får sina lärare i ett flöde. Att ordna visar vad som återstår tills klassen har kurser och elever. Koppla kurs är för en kurs som delas mellan klasser. Varje kursrad visar lärarna, och en kurs utan lärare har märket Ingen lärare. Där lägger admin också in elever, en i taget eller genom att klistra in en lista med namn och e-post (nya elever skapas utan lösenord).
- **Kursbeskrivningen** är lärarens text och valfri. Admin skriver den inte när kursen skapas.

## Inlämning och bedömning

Deadlinerna bestämmer när eleven får lämna in och vad läraren får sätta. Reglerna bor på ett ställe i backend (`GradingWindow`) och är speglade i ett ställe i klienten (`lib/gradingWindow.ts`). Beslutade i #403.

**Deadlines.** Uppgiften har en huvuddeadline och högst fem kompletteringsdeadlines (`ResubmitDueAt`). Nästa deadline är den första av dem som ligger efter nu, jämförd i UTC.

- **Fram till huvuddeadlinen** får alla lämna in och ändra sin inlämning.
- **Efter en deadline** får den som inte är godkänd och inte väntar på bedömning (Inte inlämnad, Ej inlämnat, Komplettera) lämna in fram till nästa kompletteringsdeadline, utan att vänta på läraren.
- **En Inlämnad inlämning låses** när huvuddeadlinen har gått, tills läraren har bedömt den.
- **Efter sista deadline** går inget att lämna in.

**Ej inlämnat.** Läraren sätter Ej inlämnat, helst för alla på en gång med _Markera alla som ej inlämnat_ (bara efter huvuddeadlinen). Det informerar eleven och låser inte upp något: eleven kan lämna in tills nästa deadline, precis som före.

**IG och Komplettera.** IG sätts alltid av läraren, aldrig automatiskt, och bara när ingen deadline återstår. Så länge en deadline återstår är "inte godkänd" Komplettera eller Ej inlämnat: samma nya chans, olika budskap. Komplettera kan inte begäras när ingen deadline återstår.

**Bara det senaste gäller.** Appen sparar bara den senaste bedömningen och ingen historik. Läraren kan alltid ändra en bedömning, även efter sista deadline. Det finns ingen extra deadline per elev, och ingen varning när en elev ändrar medan läraren bedömer.

### Lägen

| Läge                         | Eleven får lämna in?                       | Läraren får sätta                                          |
| ---------------------------- | ------------------------------------------ | ---------------------------------------------------------- |
| Inte inlämnad (`NotStarted`) | Ja, till nästa deadline som återstår       | Ej inlämnat, efter huvuddeadlinen                          |
| Inlämnad (`Submitted`)       | Ändra fram till huvuddeadlinen, sedan låst | G eller VG. Komplettera om en deadline återstår, annars IG |
| Komplettera (`NeedsWork`)    | Ja, till nästa kompletteringsdeadline      | IG när sista deadline har passerat                         |
| Ej inlämnat (`NotSubmitted`) | Ja, till nästa kompletteringsdeadline      | IG när sista deadline har passerat                         |
| G eller VG (`Graded`)        | Nej                                        | Rätta ett misstag                                          |
| IG (`Graded`, `Fail`)        | Nej                                        | Rätta ett misstag                                          |

### Repo-uppgifter

Repot är öppet exakt när eleven får lämna in och låst annars. Vid varje deadline, huvud- och kompletteringsdeadlines, gäller:

- **Eleven har en egen commit:** repot låses, inlämningen blir Inlämnad och `SubmissionSha` är den commiten.
- **Ingen egen commit:** repot lämnas öppet till nästa deadline. Efter sista deadline låses alla repon.

Elevens egen commit är senaste commit före deadlinen när den skiljer sig från mallens första commit (`StudentRepo.InitialSha`). Efter en öppning (`ReopenedAt`) måste den dessutom vara en ny commit, inte den som lämnades in förra gången. Komplettera öppnar ett låst repo igen. Ej inlämnat rör inte repot.

### Att bedöma

Sidan _Att bedöma_ har fyra flikar, med antalet på varje, och raderna är grupperade per uppgift:

| Flik                         | Innehåll                                                               | Läraren gör                              |
| ---------------------------- | ---------------------------------------------------------------------- | ---------------------------------------- |
| Inlämnat                     | Inlämnade inlämningar                                                  | Bedömer: G, VG, Komplettera eller IG     |
| Inte inlämnat efter deadline | Elever som inte lämnat in efter huvuddeadlinen                         | Sätter Ej inlämnat, en och en eller alla |
| Chanserna slut               | Komplettera eller Ej inlämnat när ingen deadline återstår              | Sätter IG, en i taget                    |
| Väntar på eleven             | Komplettera eller Ej inlämnat medan en deadline återstår, som översikt | Inget, eleven har en ny chans            |

Raden skiljer inte på komplettering och Ej inlämnat. Den visar _Första inlämningen_, eller _Inlämnat igen_ när en tidigare bedömning finns.

### Specialfall

- **Uppgift utan deadline (`DueAt`):** ingen deadlinelogik gäller. Inlämning står öppen tills den är bedömd och alla utfall är tillåtna.
- **På plats (`OnSite`):** läraren bedömer utan inlämning. Bara tidsreglerna för IG och Komplettera gäller.
- **Grupprepon:** gruppens medlemmar delar repo, och statusen sätts för hela gruppen när repot låses.

## Användarflöden

Ett flöde per mål. Rutorna är sidor eller steg, och rutorna med dubbla kanter är överlämningar där nästa person får veta att det är deras tur. ID:na (F1, E1 och så vidare) används i E2E-testerna och i issues. Alla flöden slutar på en sida där användaren ser att det gick bra, med en toast eller en ruta på sidan.

### F1 · Första inloggningen

Alla roller. Rollen kommer aldrig från Microsoft, bara klassen från adressen.

```mermaid
flowchart LR
    A["Logga in<br/>/login"] --> B["Microsofts inloggning"]
    B --> C{"Känner Lärka igen kontot?"}
    C -->|"Microsoft-id eller förinlagd e-post"| D["Profil första gången<br/>namnet förifyllt"]
    C -->|"Nytt konto"| E["Skapas som elev<br/>klassen från klasskoden i adressen"]
    E --> D
    D --> F{"Finns en klass?"}
    F -->|"Ja"| G["Idag, i rätt klass"]
    F -->|"Nej"| H["Du är inte med i någon klass än"]
    H ==> I(["Admin ser Elever utan klass på Idag"])
    I --> J["Admin placerar eleven eller byter roll under Användare"]
    J --> G
```

### E1 · Se vad som ska göras

```mermaid
flowchart LR
    A["Logga in"] --> B["Idag<br/>Att göra, Komplettera först"]
    B --> C["Kalender<br/>Idag, Vecka, Månad"]
    B --> D["Delen<br/>?del="]
    C --> D
    D --> E["Klarmarkera eller Lämna in"]
```

### E2 · Läsa veckan och klarmarkera

```mermaid
flowchart LR
    A["Kurs i sidomenyn"] --> B["Kursens innehåll<br/>veckor och delar"]
    B --> C["Delen<br/>Din status"]
    C --> D["Klarmarkera"]
    D --> E["Toast: Delen är klarmarkerad."]
    E --> F["Delen flyttar till Klart"]
    F --> B
    D -.->|"Ångra"| C
```

### E3 · Lämna in och komplettera

```mermaid
flowchart LR
    A["Uppgift i Att göra"] --> B["Delen<br/>deadline och betygsskala"]
    B --> C["Lämna in<br/>text, filer eller repo"]
    C --> D["Toast: Uppgiften är inlämnad."]
    D ==> E(["Läraren ser den i Att bedöma"])
    E --> F{"Läraren bedömer"}
    F -->|"Komplettera, en deadline återstår"| G(["Eleven ser Komplettera i Att göra"])
    G --> H["Läs återkopplingen och ändra"]
    H --> I["Lämna in igen till nästa deadline<br/>Toast: Uppgiften är inlämnad igen."]
    I ==> E
    F -->|"Betyg"| J["Se E4"]
```

Efter huvuddeadlinen är en Inlämnad inlämning låst tills läraren har bedömt den. Den som inte lämnat in, har fått Ej inlämnat eller ska komplettera lämnar in fram till nästa kompletteringsdeadline. Efter sista deadline är uppgiften stängd.

### E4 · Återkoppling och betyg

```mermaid
flowchart LR
    A["Avisering i Inkorg"] --> B["Delen med betyg och kommentar"]
    B --> C["Studieplan och betyg<br/>/grades"]
    C --> D["Icke godkänd, Godkänd eller Väl godkänd per kurs"]
    D -->|"Icke godkänd eller Komplettera"| E["Tillbaka till delen, se E3"]
```

### L1 · Förbereda kursen

Läraren tilldelas kursen av admin (A2). Allt kopierat och allt nytt är utkast tills läraren publicerar.

```mermaid
flowchart LR
    A(["Idag: Behöver förberedas"]) --> B["Om<br/>skriv beskrivningen,<br/>justera titel, poäng och datum"]
    B --> C["Innehåll<br/>lägg till vecka"]
    C --> D["Ny del<br/>Deadline och betygsskala"]
    D --> E["Toast: Utkastet är sparat."]
    E --> F["Publicera veckan"]
    C -.->|"eller"| G["Kopiera veckor från ett tidigare tillfälle<br/>allt blir utkast"]
    G --> F
    F ==> H(["Eleverna ser delarna på sina datum"])
```

### L2 · Köra veckan

```mermaid
flowchart LR
    A["Kursen"] --> B["Delens sida<br/>läsbar först"]
    B --> C["Redigera"]
    C --> D["Spara<br/>Toast: Ändringarna i delen är sparade."]
    D --> B
    A --> E["Nyheter"]
    E --> F["Publicera nyhet<br/>Toast: Nyheten är publicerad."]
    F ==> G(["Eleverna ser den i Inkorg och på Idag"])
    G -.-> H["Kommentar<br/>Toast: Kommentaren är skickad."]
```

### L3 · Bedöma

```mermaid
flowchart LR
    A["Att bedöma<br/>/grading, fyra flikar"] --> B["Inlämning<br/>/assignments/:linkId/submissions/:id"]
    B --> C["Betyg och kommentar<br/>Icke godkänd kräver en kommentar"]
    C --> D["Spara och nästa<br/>Bedömningen är sparad."]
    D -->|"fler i kön"| B
    D -->|"kön är tom"| A
    D ==> E(["Eleven ser betyget i Inkorg, E4"])
```

Läraren väljer flik efter vad som väntar: Inlämnat bedöms, Inte inlämnat efter deadline får Ej inlämnat, och Chanserna slut får IG. Se _Inlämning och bedömning_ ovan.

### A1 · Sätta upp klassen

```mermaid
flowchart LR
    A["Skolans IT skapar adresserna<br/>namn.efternamn.net25@..."] --> B["Ny klass<br/>/groups"]
    B --> D["Lägg till medlemmar<br/>en i taget eller klistra in en lista"]
    D --> E["Toast: ... är tillagd i klassen."]
    A ==> F(["Eleverna loggar in, F1, och hamnar i klassen via koden"])
    F --> G["Elever utan klass på Idag, se A5"]
```

### A2 · Planera terminen

```mermaid
flowchart LR
    A["Klassens sida"] --> B["Ny kurs<br/>utgå från en tidigare kurs eller en tom"]
    B --> C["Namn, kod, datum och lärare<br/>kopian får förslag på eget namn och egen kod"]
    C --> D["Kursen skapas som utkast"]
    D ==> E(["Läraren ser Behöver förberedas, L1"])
    A -.->|"delad kurs"| F["Koppla kurs<br/>Toast: ... är kopplad till klassen."]
    F --> G["Lärarraden syns direkt"]
```

### A3 · Byta lärare

```mermaid
flowchart LR
    A["Klassens sida"] --> B["Ändra lärare på kursraden"]
    B --> C{"Delas kursen med en annan klass?"}
    C -->|"ja"| D["Dialogen säger vilka klasser som påverkas"]
    C -->|"nej"| E["Lägg till vikarien"]
    D --> E
    E --> F["Ta bort den sjuka läraren"]
    F ==> G(["Vikarien ser kursen under Behöver förberedas eller i Mina kurser"])
```

### A4 · Stänga terminen

```mermaid
flowchart LR
    A["Läraren bedömer klart, L3"] --> B["Kursens slutdatum passeras"]
    B --> C["Kursen och klassen visas under Avslutade"]
    C --> D["Ny kurs, utgå från den gamla, A2"]
```

Ingen stänger terminen med en knapp. Avslutade kurser och klasser följer av datumen och flyttas till en hopfälld grupp _Avslutade_ i sidomenyn, i Alla grupper, på klassens sida, i kalenderns kursval och bland materialets flikar (_Visa avslutade_). Idag visar eleven bara pågående och kommande kurser, och läraren bara pågående kurser och avslutade med inlämningar kvar att bedöma. Avslutade kurser och klasser räknas inte som saknad lärare i _Att ordna_. Inget tas bort och ingen förlorar åtkomst: länkar, material, betyg, studieplan, _Att bedöma_ och sökning når allt. Kurser utan datum räknas som pågående.

### A5 · Ordna det som saknas

```mermaid
flowchart LR
    A["Idag<br/>Att ordna"] --> B["Klass eller kurs"]
    B --> C["Utan klass, Ingen lärare eller Kan inte logga in"]
    C --> D["Åtgärd: placera eller tilldela"]
    D --> E["Toast med vad som sparades"]
    E --> F["Att ordna är tomt"]
```

### Fel och behörighet

En sida som kräver en behörighet användaren saknar öppnas inte. Användaren hamnar på Idag med toasten "Du har inte behörighet till den sidan." Fel från servern visas alltid på svenska: backends korta engelska meddelanden översätts i klienten (`client/src/lib/serverMessages.ts`), och okända fel får en allmän text efter statuskoden.
