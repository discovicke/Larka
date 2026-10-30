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
| E2E                  | `e2e/` (Playwright)                       | varje dag 05:17 UTC och manuellt            | den deployade appen |

### Backend mot Postgres

Testerna startar en tillfällig Postgres med Testcontainers (en container per körning, en databas per testklass) och kör migrationerna, så `dotnet test` kräver att Docker körs.

### E2E mot den deployade appen

[`e2e/`](../e2e) kör mot den deployade appen: att `/health` och `/version` svarar, att startsidan och sidomenyn laddar, att en kurs går att öppna med nyhetsfliken, att `Alla grupper` visar tabellen utan sidledes scroll, att en okänd sökväg visar 404 och att utloggad skickas från `/groups` till inloggningen. Varje test körs både som desktop och som mobil. Starta manuellt via _Actions › E2E › Run workflow_.

Testerna **läser bara** appen: de skapar, ändrar eller tar aldrig bort något i produktion. De loggar in som demokontot `elev`.

```bash
cd e2e
npm ci
npx playwright install chromium

npx playwright test                                        # mot den deployade appen
E2E_BASE_URL=http://localhost:5173 npx playwright test     # mot den lokala dev-instansen
E2E_USERNAME=larare E2E_PASSWORD=larare npx playwright test # som ett annat konto
```

Render-instansen somnar efter 15 minuter utan trafik, så `global-setup.ts` väntar tills `/health` svarar innan testerna börjar (upp till 3 minuter). Sedan loggar den in och sparar tokenet i `e2e/.auth/token.json` (checkas inte in), som testernas fixture lägger i sessionStorage precis som klienten.
