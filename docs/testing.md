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
- Dev-verktygen under `components/dev` är inte kärna och testas inte.
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
- dev-only formulär och sidor

`npm run test:coverage` räknar bara produktionskod. Genererade UI-komponenter och dev-verktyg är undantagna i `vitest.config.ts`.

## Testnivåer och hur de körs

| Nivå                 | Var                                       | Körs                                               | Kräver              |
| -------------------- | ----------------------------------------- | -------------------------------------------------- | ------------------- |
| Backend              | `backend.Tests/` (xUnit)                  | `dotnet test RebornLMS.sln`, på varje PR           | Docker              |
| Egna analyzer-regler | `backend.Analyzers.Tests/`                | samma                                              |                     |
| Frontend             | `client/src/**/*.spec.ts` (Vitest, jsdom) | `npm --prefix client run test`, på varje PR        |                     |
| Layout               | `client/layout/` (Playwright)             | `npm --prefix client run test:layout`, på varje PR | Chromium            |
| E2E                  | `e2e/` (Playwright)                       | varje dag 05:17 UTC och manuellt                   | den deployade appen |

### Backend mot Postgres

Testerna startar en tillfällig Postgres med Testcontainers (en container per körning, en databas per testklass) och kör migrationerna, så `dotnet test` kräver att Docker körs.

### Layout i webbläsaren

Vitest kör i jsdom, som saknar layout, så sidornas mått prövas i en riktig Chromium med Playwright. Testerna startar `vite preview` av det byggda paketet och mockar `/api/**` (`layout/mocks.ts`), så varken backend eller databas behövs. De kontrollerar att Idag, kursens innehåll, skapa innehåll (alla typer av del) och 404 inte scrollar i sidled vid 390 och 1440 px, att headern är minst 56 px, sidomenyn 256 px och innehållet högst 1120 px, och att sidomenyn är en drawer under 768 px. I CI laddas rapporten upp som artefakt om något faller.

```bash
cd client
npx playwright install chromium   # en gång
npm run test:layout               # bygger och kör testerna
```

Helsidesbilder vid 390 och 1440 px, t.ex. till en PR: `SCREENSHOTS=1 npx playwright test screenshots`. Bilderna hamnar i `client/layout/screenshots/` (byt med `SCREENSHOT_DIR`, och sidorna med `SCREENSHOT_ROUTES=/,/courses/c1/content`).

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
