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
| E2E                  | `e2e/tests/` (Playwright)                 | varje natt 02:23 UTC och manuellt           | Docker (egen stack) |

### Backend mot Postgres

Testerna startar en tillfällig Postgres med Testcontainers (en container per körning, en databas per testklass) och kör migrationerna, så `dotnet test` kräver att Docker körs.

### E2E: tre användarflöden

[`e2e/`](../e2e) har tre Playwright-tester som tillsammans följer kärnan i Lärka: admin förbereder en kurs, läraren publicerar en uppgift, eleven lämnar in, läraren bedömer och eleven ser betyget.

| #   | Flöde                                                                   | Fil                                             |
| --- | ----------------------------------------------------------------------- | ----------------------------------------------- |
| 1   | Admin skapar en klass, klistrar in elever och skapar en kurs med lärare | `1-admin-forbereder-kursen.spec.ts`             |
| 2   | Läraren publicerar en uppgift i en ny vecka, eleven lämnar in           | `2-lararen-publicerar-eleven-lamnar-in.spec.ts` |
| 3   | Läraren bedömer från Att bedöma, eleven ser betyget och kommentaren     | `3-lararen-bedomer-eleven-ser-betyget.spec.ts`  |

Testerna går igenom det riktiga gränssnittet och byter roll mitt i testet där flödet har en överlämning. Data som ett test bara behöver förbereda (lärare, elever, en inlämning att bedöma) skapas via API:et med unika namn, så varje test är oberoende av de andra och av demodatan. Testerna körs i tur och ordning (`workers: 1`).

**Testerna skriver data och körs aldrig mot produktion.** De körs mot en färsk stack: Postgres och appen byggd av `Dockerfile`, med `Database__SeedDemoData=true` och engångsvärden för hemligheterna. Standardadressen är `http://localhost:8080`, och `playwright.config.ts` vägrar starta om `E2E_BASE_URL` pekar på `onrender.com`. Att den deployade appen fungerar kontrolleras i stället av CD, som väntar tills produktionens `/version` visar den nya versionen.

E2E körs inte på PR:er. Där körs bara bygge, formatering, lint och enhetstester, så att en PR inte väntar på en hel stack. Workflowet `e2e.yml` körs varje natt 02:23 UTC mot det som finns på `main`, och manuellt via _Actions › E2E › Run workflow_ före en riskabel merge.

Lokalt:

```bash
docker compose -f e2e/docker-compose.e2e.yml up -d --build --wait   # E2E_APP_PORT=5303 om 8080 är upptagen
cd e2e
npm ci
npx playwright install chromium
npx playwright test                                                 # E2E_BASE_URL=http://localhost:5303 vid annan port
cd ..
docker compose -f e2e/docker-compose.e2e.yml down -v               # ta alltid bort databasen
```

Global setup väntar tills `/health` svarar och loggar in som demokontot `admin`. Tokenet sparas i `e2e/.auth/token-admin.json` (checkas inte in).
