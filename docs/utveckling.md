# Utveckling

Hur man kör projektet lokalt och vad som måste göras när databasen eller API:et ändras. Kom igång-stegen finns i [README](../README.md#kom-igång).

## Dev-servern

`npm run dev` startar backend (http://localhost:5257) och Vite (http://localhost:5173) samtidigt. Öppna Vite-adressen. Vite skickar `/api`, `/health` och `/version` vidare till backend, så webbläsaren pratar bara med en origin och ingen CORS behövs.

- API:et ligger under `/api`, t.ex. `/api/groups`. `/health` och `/version` ligger på roten.
- Swagger UI finns på http://localhost:5257/swagger och OpenAPI-dokumentet på http://localhost:5257/openapi/v1.json. Logga in med `/api/auth/login` och klistra in tokenet under _Authorize_.

## Databas

Lokalt kör backend mot Postgres i Docker (`npm run db`, data sparas i en Docker-volym). I prod är det Supabase Postgres. Testerna startar en egen Postgres med Testcontainers, se [testing.md](testing.md).

Vid start kör backend de migrationer som saknas (`Database.Migrate()`), både lokalt och i prod. Render free tier har inget pre-deploy-steg, så det sker när appen startar. En tom lokal databas får kompakt demodata från [`DemoSeed`](../backend/Infrastructure/Persistence/Seeding/DemoSeed.cs) när `Database:SeedDemoData` är `true`, se [inloggningar](auth-och-behorigheter.md#demokonton-development-och-demon).

**Ändrat en entitet eller en konfiguration?** Skapa en migration och commita den:

```bash
dotnet ef migrations add <VadSomÄndrats> --project backend
```

Efter migrationerna slår backend på row level security för alla tabeller i `public` som saknar det ([`RowLevelSecurity`](../backend/Infrastructure/Persistence/RowLevelSecurity.cs)). Supabase lämnar ut `public` via sitt REST-API, och utan RLS kunde vem som helst med projektets publika nyckel läsa tabellerna. Appen är tabellernas ägare och påverkas inte. En ny tabell behöver alltså inget eget steg. Testet `RowLevelSecurityTests` kontrollerar det.

Testet `Migrations_match_the_model` faller i CI om en ändring saknar migration. Migrationerna ligger i `backend/Infrastructure/Persistence/Migrations/` och ska inte ändras för hand. Slås migrationerna ihop till en ny baslinje måste prod-databasen återställas, eftersom den annars försöker köra baslinjen ovanpå tabeller som redan finns.

Börja om från en tom lokal databas: `docker compose down -v && npm run db`.

Hemligheter behövs inte lokalt. Vill du köra mot något annat, till exempel Supabase eller GitHub-sandlådan, lägg nycklarna i Infisical (projektet Lärka, miljön `dev`) och starta backend genom [Infisicals CLI](https://infisical.com/docs/cli/overview):

```bash
infisical login                                # välj Infisical Cloud (EU Region), sparas till nästa gång
infisical run -- dotnet run --project backend
```

`infisical run` hämtar hemligheterna och ger dem som miljövariabler till kommandot efter `--`, och bara till det. Inget skrivs till disk, och terminalen får dem inte. Projektet läses från `.infisical.json`. `--env=prod` eller `--path=/deploy` väljer en annan miljö eller mapp.

Alla inställningar finns i [konfiguration.md](konfiguration.md).

## API-typer i klienten

C#-DTO:erna i backend är den enda källan. Varje `dotnet build` (Debug) skriver OpenAPI-dokumentet till `client/openapi.json`, och `npm run gen:api` gör om det till TypeScript-typer i `client/src/lib/api-types.ts`. Båda filerna genereras och ska inte ändras för hand.

**Ändrat en DTO eller en endpoint?** Kör båda stegen och commita de två filerna:

```bash
dotnet build backend && npm --prefix client run gen:api
```

CI faller om någon av filerna inte stämmer med koden.

Anropa API:et från klienten med `api` i `client/src/lib/api.ts`:

```ts
const {data} = await api.GET('/api/groups/{id}', {params: {path: {id}}})
```

Sökvägar, parametrar, bodies och svar är typade. Fel kastas som `ApiError` med HTTP-status. En DTO som typ: `Schema<'GroupDto'>`.

## Publicera lokalt

```bash
dotnet publish backend -c Release -o publish
```

Publish bygger klienten (`npm ci` + `npm run build`) och lägger den i `wwwroot`, så backend serverar både API:et och klienten. Sökvägar som inte är filer eller `/api` får klientens `index.html`. Docker-imagen byggs på samma sätt, se [deploy.md](deploy.md).
