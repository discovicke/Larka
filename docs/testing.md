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
