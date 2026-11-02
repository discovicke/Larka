# Teststrategi

Vi testar det som är kärnan i produkten, inte varje rad kod. 100 % täckning är inte ett mål.

## Kärnan

- publicering och livscykel för veckor, delar och nyheter
- elevvyn: låsningar, upplåsning och klarmarkering
- inlämningar, bedömning och återkoppling
- bilagor och filuppladdning
- behörigheter och roller när inloggningen finns
- rena regler som roadmap, publiceringsdatum och formatfunktioner

## Frontend

- Använd `renderApp` och `mockApi` för att testa användarens flöde genom vyerna.
- Svara med `respond<'/api/courses', 'get'>(body)` i `mockApi` så att fixturen typkontrolleras mot OpenAPI-schemat (se `CalendarView.spec.ts`).
- Asserta på synlig text, roller, `data-test` och skickade API-anrop.
- Undvik att asserta på CSS-klasser, intern komponentstruktur eller exakta implementationdetaljer.
- Genererade shadcn-komponenter under `components/ui` testas indirekt genom vyerna.

## Backend

- Endpoint-tester mot riktig Postgres är den viktigaste nivån: HTTP-status, validering, behörighet och sparat tillstånd.
- Rena policies och mappningar testas som enhetstester.
- Behåll skyddsnät för migrationer, OpenAPI-kontraktet och databasrelationer.
- Lägg inte till separata tester för varje trivial getter eller CRUD-variant om endpointtestet redan täcker beteendet.

## Inte testmål

- 100 % kodtäckning
- styling och exakta CSS-klasser
- genererade UI-komponenter

`npm run test:coverage` räknar bara produktionskod. Genererade UI-komponenter är undantagna i `vitest.config.ts`.

## Kodtäckning

Täckning är en signal, inte ett mål: den visar vilka delar som saknar tester, inte om testerna är bra. Ingen tröskel fäller bygget.

```bash
npm run coverage
```

Kommandot kör backendtesterna (Docker måste vara igång) och frontendtesterna med täckning och slår ihop dem med ReportGenerator (ett lokalt dotnet-verktyg i `dotnet-tools.json`). Rapporten hamnar i `coverage/report/index.html`, med en sammanfattning i `coverage/report/Summary.txt`. Mapparna `coverage/` och `client/coverage/` är ignorerade av git.

Undantaget från täckningen:

- Backend (`coverlet.runsettings`): EF-migreringar (genererade), `Program.cs` (uppstartskod), `DevSeed.cs` (dev-data), kod märkt `[GeneratedCode]` eller `[CompilerGenerated]` samt testprojekten och analyzern.
- Frontend (`client/vitest.config.ts`): genererade UI-komponenter, `main.ts`, `utils.ts` och genererade API-typer.

I CI samlar backend- och frontendjobben varsin täckning och skriver sitt tal i jobbsammanfattningen. Jobbet `Coverage report` slår ihop dem. Sammanfattningen finns i det jobbet och hela HTML-rapporten i artefakten `coverage-report`. Jobbet är inget obligatoriskt krav. README:ns badge visar radtäckningen för hela repot och uppdateras av CD vid varje körning på `main`.

## Testnivåer och hur de körs

| Nivå                 | Var                                       | Körs                                        | Kräver              |
| -------------------- | ----------------------------------------- | ------------------------------------------- | ------------------- |
| Backend              | `backend.Tests/` (xUnit)                  | `dotnet test Larka.sln`, på varje PR        | Docker              |
| Egna analyzer-regler | `backend.Analyzers.Tests/`                | samma                                       |                     |
| Frontend             | `client/src/**/*.spec.ts` (Vitest, jsdom) | `npm --prefix client run test`, på varje PR |                     |
| E2E smoke            | `e2e/tests/smoke.spec.ts` (Playwright)    | varje dag 05:17 UTC och manuellt            | den deployade appen |
| E2E journeys         | `e2e/tests/journeys/` (Playwright)        | varje natt 02:23 UTC och manuellt           | Docker (egen stack) |

### Backend mot Postgres

Testerna startar en tillfällig Postgres med Testcontainers (en container per körning, en databas per testklass) och kör migrationerna, så `dotnet test` kräver att Docker körs.

### E2E: två sviter

[`e2e/`](../e2e) har två sviter i samma Playwright-uppsättning. Ingen av dem körs på PR:er. Där körs bara bygge, formatering, lint och enhetstester, så att en PR inte väntar på en hel E2E-stack. E2E körs på schema mot det som redan finns på `main`, och manuellt via _Actions_ före en riskabel merge.

|          | smoke                              | journeys                                             |
| -------- | ---------------------------------- | ---------------------------------------------------- |
| Vad      | att appen lever och sidorna laddar | användarflödena från början till slut                |
| Skriver  | **nej**, bara läsning              | **ja**, skapar klasser, kurser, inlämningar och mer  |
| Mot vad  | den deployade appen (prod)         | en färsk lokal stack, **aldrig prod**                |
| Workflow | `e2e.yml`, dagligen 05:17 UTC      | `e2e-journeys.yml`, nattligen 02:23 UTC              |
| Projekt  | `chromium` och `mobile`            | `journeys` (desktop) och `journeys-mobile` (bara E1) |

Journeys är avstängda om inte `E2E_JOURNEYS=1` är satt, och konfigurationen vägrar starta om basadressen är `onrender.com`. Samma kontroll finns som en automatisk fixture som fäller varje journey som körs mot produktion.

#### Smoke mot den deployade appen

Testerna kontrollerar att `/health` och `/version` svarar, att startsidan och sidomenyn laddar, att en kurs går att öppna med nyhetsfliken, att `Alla grupper` visar tabellen utan sidledes scroll, att en okänd sökväg visar 404 och att utloggad skickas från `/groups` till inloggningen. Dessutom loggar de in som lärare (Idag och Att bedöma) och som admin (Idag och Användare). Varje test körs både som desktop och som mobil. Starta manuellt via _Actions › E2E › Run workflow_.

Testerna **läser bara** appen: de skapar, ändrar eller tar aldrig bort något i produktion. Global setup loggar in som varje demokonto (`elev`, `larare`, `admin`) och sparar en token per roll i `e2e/.auth/token-<roll>.json` (checkas inte in). Ett test väljer roll med `test.use({role: 'larare'})`. Konton byts med `E2E_<ROLL>_USERNAME` och `E2E_<ROLL>_PASSWORD` (till exempel `E2E_LARARE_USERNAME`). För eleven gäller också de gamla `E2E_USERNAME` och `E2E_PASSWORD`.

```bash
cd e2e
npm ci
npx playwright install chromium

npx playwright test                                        # smoke mot den deployade appen
E2E_BASE_URL=http://localhost:5173 npx playwright test     # mot den lokala dev-instansen
E2E_ELEV_USERNAME=elev2 E2E_ELEV_PASSWORD=hemligt npx playwright test # som ett annat elevkonto
```

Render-instansen somnar efter 15 minuter utan trafik, så `global-setup.ts` väntar tills `/health` svarar innan testerna börjar (upp till 3 minuter).

#### Journeys mot en färsk stack

Varje journey har en egen fil under `e2e/tests/journeys/`, och testets titel börjar med flödets ID. Testet går igenom det riktiga gränssnittet och byter roll mitt i testet där flödet har en överlämning (läraren bedömer, eleven ser resultatet). Data som ett test bara behöver förbereda skapas via API:et med unika namn, så varje test är oberoende av de andra och av demodatan. Journeys körs i tur och ordning (`workers: 1`).

Lokalt, mot en stack byggd av `Dockerfile`:

```bash
docker compose -f e2e/docker-compose.e2e.yml up -d --build --wait   # E2E_APP_PORT=5303 om 8080 är upptagen
cd e2e
E2E_BASE_URL=http://localhost:8080 E2E_JOURNEYS=1 npx playwright test --project=journeys --project=journeys-mobile
cd ..
docker compose -f e2e/docker-compose.e2e.yml down -v               # ta alltid bort databasen
```

Stacken är Postgres och appen med `Database__SeedDemoData=true` och engångsvärden för hemligheterna (JWT-nyckel, fillagring). Det går också att köra mot en app som startats på annat sätt, till exempel `dotnet publish` med en egen databas, så länge den har demodata.

**Journeys skriver data och körs aldrig mot produktion.** Använd aldrig `E2E_BASE_URL` med Render-adressen tillsammans med `E2E_JOURNEYS=1`.

| ID  | Flöde                                                           | Fil                              |
| --- | --------------------------------------------------------------- | -------------------------------- |
| F1  | Första inloggningen med Microsoft, välkomststeg, klass eller ej | `f1-forsta-inloggningen.spec.ts` |
| E1  | Idag, Kalender och mobil visar vad som ska göras (även mobil)   | `e1-se-vad-jag-ska-gora.spec.ts` |
| E2  | Läsa veckan och klarmarkera en del                              | `e2-lasa-veckan.spec.ts`         |
| E3  | Lämna in, komplettera och lämna in igen                         | `e3-lamna-in.spec.ts`            |
| E4  | Se återkoppling och betyg                                       | `e4-se-betyg.spec.ts`            |
| L1  | Förbereda kursen: beskrivning, vecka, del, publicera            | `l1-forbered-kursen.spec.ts`     |
| L2  | Köra veckan: ändra en del, skriva en nyhet                      | `l2-kor-veckan.spec.ts`          |
| L3  | Bedöma: Spara och nästa, IG kräver kommentar                    | `l3-bedom.spec.ts`               |
| M1  | Mentorn läser klassen utan att kunna ändra                      | `m1-folj-klassen.spec.ts`        |
| A1  | Skapa klass och klistra in elever                               | `a1-skapa-klass.spec.ts`         |
| A2  | Ny kurs från klassen och Koppla kurs                            | `a2-planera-terminen.spec.ts`    |
| A3  | Ersätta lärare, bekräfta innan den sista tas bort               | `a3-byt-larare.spec.ts`          |
| A4  | Avsluta en kurs, den hamnar under Avslutade                     | `a4-avsluta-terminen.spec.ts`    |
| A5  | Idag, Att ordna per klass med länkar                            | `a5-att-ordna.spec.ts`           |

F1 kan inte köra riktiga Microsoft. Testet ersätter MSAL-biblioteket (som är en egen chunk i bygget) med en stub som skickar tillbaka till `/login` med en kod, och svaret från `POST /api/auth/microsoft` med en token från vanlig inloggning och `firstSignIn: true`. Inloggningssidan, välkomststeget, profilen och sidorna efter är de riktiga. Kontrollen av ID-tokenet på servern täcks inte av testet, den ligger i backendtesterna.
