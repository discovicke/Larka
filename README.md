# net25-devops-grupp-4

[![CD](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml/badge.svg)](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml)

Live: https://net25-devops-grupp-4.onrender.com (gratisinstansen somnar efter 15 minuter utan trafik, första anropet kan ta en minut)

Design: [mockup](https://claude.ai/artifact/PPjCF4RnekzyfrvJrYF8LC), se [docs/design.md](docs/design.md).

## Om projektet

RebornLMS är ett lärplattformssystem (LMS) för skolor. Lärare skapar kurser och innehåll (nyheter, teori och uppgifter) och publicerar det till grupper. Studenter lämnar in uppgifter, får betyg och kommentarer och notiser. Admin hanterar skolor, användare och grupper.

| Del         | Teknik                                      | Mapp                 |
| ----------- | ------------------------------------------- | -------------------- |
| Backend     | ASP.NET Core (.NET 10), EF Core, Postgres   | `backend/`           |
| Klient      | Vue 3, Vite, TypeScript, shadcn-vue         | `client/`            |
| Tester      | xUnit (Testcontainers mot Postgres), Vitest | `backend.Tests/`     |
| Egna regler | Roslyn-analyzers för kodstilen              | `backend.Analyzers/` |

Datamodellen finns i [docs/er-diagram.md](docs/er-diagram.md) och designen i [docs/design.md](docs/design.md).

## Köra lokalt

Du behöver:

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 24](https://nodejs.org/) med npm
- [Docker](https://www.docker.com/) (Postgres lokalt och i `dotnet test`)

```bash
npm install            # i repots rot, en gång (husky + concurrently)
npm --prefix client install
dotnet tool restore    # en gång, installerar dotnet-ef
npm run db             # startar Postgres i Docker (docker-compose.yml)
npm run dev
```

`npm run dev` startar backend (http://localhost:5257) och Vite (http://localhost:5173) samtidigt. Öppna Vite-adressen. Vite skickar `/api`, `/health` och `/version` vidare till backend, så webbläsaren pratar bara med en origin och ingen CORS behövs.

- API:et ligger under `/api`, t.ex. `/api/groups`. `/health` och `/version` ligger på roten.
- Anropa API:et från klienten med `api` i `client/src/lib/api.ts`, t.ex. `const {data} = await api.GET('/api/groups/{id}', {params: {path: {id}}})`. Sökvägar, parametrar, bodies och svar är typade från backendens OpenAPI-dokument. Fel kastas som `ApiError` med HTTP-status. En DTO som typ: `Schema<'GroupDto'>`.
- Swagger UI finns på http://localhost:5257/swagger och OpenAPI-dokumentet på http://localhost:5257/openapi/v1.json.

### Databas

Lokalt kör backend mot Postgres i Docker (`npm run db`, data sparas i en Docker-volym). I prod är det Supabase Postgres. Testerna startar en tillfällig Postgres med Testcontainers (en container per körning, en databas per testklass) och kör migrationerna, så `dotnet test` kräver att Docker körs.

Vid start kör backend de migrationer som saknas (`Database.Migrate()`), både lokalt och i prod. Render free tier har inget pre-deploy-steg, så det sker när appen startar. En tom lokal databas får också exempeldata (en klass, två kurser och lite innehåll) från [`DevSeed`](backend/Infrastructure/Persistence/DevSeed.cs).

**Ändrat en entitet eller en konfiguration?** Skapa en migration och commita den:

```bash
dotnet ef migrations add <VadSomÄndrats> --project backend
```

Testet `Migrations_match_the_model` faller i CI om en ändring saknar migration. Migrationerna ligger i `backend/Infrastructure/Persistence/Migrations/` och ska inte ändras för hand.

Börja om från en tom lokal databas: `docker compose down -v && npm run db`.

Vill du köra mot en annan databas, t.ex. Supabase, lägg connection string i user-secrets så att den aldrig hamnar i repot. Stäng av exempeldatan, annars läggs den in om databasen saknar grupper:

```bash
dotnet user-secrets set ConnectionStrings:Default "Host=...;Port=5432;..." --project backend
dotnet user-secrets set Database:SeedDevData false --project backend
```

### Fillagring

Filer (bilagor och inlämningar) ligger i en privat bucket i Supabase Storage, bakom `IFileStorage`. Databasen sparar bara nyckeln (`content/{contentId}/{guid}`, `submissions/{submissionId}/{guid}`), och svaren innehåller en länk som gäller i 10 minuter och skapas när filen läses.

Uppladdning sker med `multipart/form-data`: bilagor till innehåll med `POST /api/content/{id}/attachments` (fältet `file`), och inlämningar med `POST /api/links/{id}/submissions` (fälten `bodyMd` och `files`). Högst 20 MB per fil, 10 filer och 50 MB per anrop, och bara filtyper som finns i `FileUploadPolicy` (pdf, bilder, txt, md, Office-filer och zip). Typen sätts från filändelsen, inte från vad klienten skickar. Eleven ser sina egna filer, och lärarna på kursen och admin ser alla inlämningars filer.

Utan inställningar lagras filerna i minnet i Development (borta när appen stängs av), och i andra miljöer går det inte att spara eller läsa filer. Skapa en privat bucket, t.ex. `files`, i Supabase-projektet och ställ in:

```bash
dotnet user-secrets set Storage:Supabase:Url "https://<projekt>.supabase.co" --project backend
dotnet user-secrets set Storage:Supabase:ServiceRoleKey "<service role key>" --project backend
```

Bucketens namn är `Storage:Supabase:Bucket` (standard `files`). Service role-nyckeln går förbi Supabases row level security och är en hemlighet: user-secrets lokalt och miljövariabeln `Storage__Supabase__ServiceRoleKey` på Render, aldrig i `appsettings`. I produktion ligger de som `SUPABASE_URL` (variabel) och `SUPABASE_SERVICE_ROLE_KEY` (secret) i GitHub-miljön `production`, och deploy-jobbet skickar dem vidare till Render. Åtkomstkontrollerna görs i backend innan en länk skapas.

### API-typer i klienten

C#-DTO:erna i backend är den enda källan. Varje `dotnet build` (Debug) skriver OpenAPI-dokumentet till `client/openapi.json`, och `npm run gen:api` gör om det till TypeScript-typer i `client/src/lib/api-types.ts`. Båda filerna genereras och ska inte ändras för hand.

**Ändrat en DTO eller en endpoint?** Kör båda stegen och commita de två filerna:

```bash
dotnet build backend && npm --prefix client run gen:api
```

CI faller om någon av filerna inte stämmer med koden.

## Publicera

```bash
dotnet publish backend -c Release -o publish
```

Publish bygger klienten (`npm ci` + `npm run build`) och lägger den i `wwwroot`, så backend serverar både API:et och klienten. Sökvägar som inte är filer eller `/api` får klientens `index.html`.

## Deploy

Allt som når `main` deployas automatiskt av [`cd.yml`](.github/workflows/cd.yml):

1. **checks**: samma kontroller som på PR:er
2. **release**: semantic-release sätter versionstagg (se [Versioner och releaser](#versioner-och-releaser))
3. **image**: bygger Docker-imagen och pushar den till `ghcr.io/eduedugrade/net25-devops-grupp-4`, taggad med commit, version och `latest`
4. **deploy**: säger åt Render att köra imagen, väntar tills den är live och kontrollerar att `/version` svarar med rätt commit

Render kör imagen i Frankfurt, databasen är Supabase Postgres i samma region. Misslyckas en deploy behåller Render den gamla versionen live. `GET /version` visar vilken version och commit som körs.

Tjänsten skapas och konfigureras med [`infra/render-setup.sh`](infra/render-setup.sh). GitHub-miljön `production` innehåller `RENDER_API_KEY` och `RENDER_SERVICE_ID` (secrets) och `APP_URL` (variabel).

## Bidra

Kör `npm install` i repots rot en gång efter kloning. Det aktiverar git-hooken som kontrollerar commit-meddelanden.

### Commit-format

Vi använder [conventional commits](https://www.conventionalcommits.org/). Commits och PR-titlar ska se ut så här:

```
<typ>(<valfritt scope>): <kort beskrivning>
```

Vanliga typer: `feat` (ny funktion), `fix` (buggfix), `docs`, `test`, `refactor`, `style`, `build`, `ci`, `chore`.

Exempel: `feat(client): add course page`, `fix(backend): return 404 for missing course`.

PR:er squash-mergas, så PR-titeln blir commiten på `main` och kontrolleras i CI.

### Versioner och releaser

Varje push till `main` kör först samma kontroller som på PR:er och sedan [semantic-release](https://semantic-release.gitbook.io/), som läser commits sedan förra taggen och räknar fram nästa version enligt [semver](https://semver.org/):

| Commit på `main`                                      | Version       |
| ----------------------------------------------------- | ------------- |
| `fix: ...`                                            | patch 1.0.x   |
| `feat: ...`                                           | minor 1.x.0   |
| `feat!: ...` eller `BREAKING CHANGE:` i commit-texten | major x.0.0   |
| `docs`, `test`, `ci`, `chore` m.fl.                   | ingen release |

Den skapar en git-tagg (`v1.2.3`) och en GitHub-release med release notes. Inget commitas tillbaka till `main`, så branch-skyddet behöver inga undantag.

### Kontroller på varje PR

CI kör följande och alla måste vara gröna innan en PR kan mergas. Kör dem lokalt innan du pushar:

```bash
# Formatering i hela repot (YAML, JSON, Markdown, CSS, HTML)
npx prettier --check .

# Backend
dotnet build backend.Analyzers   # våra egna regler, måste finnas innan dotnet format körs
dotnet format RebornLMS.sln --verify-no-changes
dotnet build RebornLMS.sln       # skriver också client/openapi.json
dotnet test RebornLMS.sln

# Frontend
cd client
npm run gen:api                  # API-typerna från client/openapi.json
npm run lint
npm run build
npm run test
```

`npm run format` i repots rot fixar formateringen i hela repot (Prettier, ESLint och `dotnet format`), och `npm run format:check` kontrollerar den.

### E2E mot den deployade appen

[`e2e/`](e2e) innehåller Playwright-tester som kör mot den deployade appen: att `/health` och `/version` svarar, att startsidan och sidomenyn laddar, att en kurs går att öppna med nyhetsfliken, att `Alla grupper` visar tabellen utan sidledes scroll och att en okänd sökväg visar 404. Varje test körs både som desktop och som mobil.

De körs varje dag 05:17 UTC och manuellt via _Actions › E2E › Run workflow_. Testerna **läser bara** appen: de skapar, ändrar eller tar aldrig bort något i produktion, och de kräver ingen inloggning. Adressen kommer från repository-variabeln `E2E_BASE_URL`, med https://net25-devops-grupp-4.onrender.com som standard.

```bash
cd e2e
npm ci
npx playwright install chromium

# Mot den deployade appen
npx playwright test

# Mot den lokala dev-instansen
E2E_BASE_URL=http://localhost:5173 npx playwright test
```

Render-instansen somnar efter 15 minuter utan trafik, så `global-setup.ts` väntar tills `/health` svarar innan testerna börjar (upp till 3 minuter).

### Kodstil

Tre verktyg delar på formateringen, och `npm run format` kör alla:

| Filer                              | Verktyg                                                                                   | Inställningar                         |
| ---------------------------------- | ----------------------------------------------------------------------------------------- | ------------------------------------- |
| JS, TS och Vue                     | ESLint ([ESLint Stylistic](https://eslint.style/) och layoutreglerna i eslint-plugin-vue) | `client/eslint.config.js`             |
| YAML, JSON, Markdown, CSS och HTML | Prettier                                                                                  | `.prettierrc.json`, `.prettierignore` |
| C#                                 | `dotnet format`                                                                           | `.editorconfig`                       |

Några av ESLint-reglerna:

```ts
import {computed} from 'vue'   // inga mellanslag innanför { }

return value === ''            // ? och : alltid på egna rader
  ? null
  : value

groups.map((group) => group.id) // inga enbokstavsnamn, utom i i loopar, T och _
```

```vue
<Button/>                      <!-- inget mellanslag före /> -->
<input type="text"/>
```

I C# gäller även regler som `dotnet format` inte har själv: max 120 tecken per rad, `?` och `:` i en ternary på egna rader, inga namn på en bokstav och inga property patterns. Se [docs/code-style-csharp.md](docs/code-style-csharp.md).

Ställ in editorn så att den formaterar med samma verktyg som CI. I VS Code: installera tilläggen ESLint, Prettier och C# Dev Kit, och slå på _Format on Save_ (ESLint fixar JS, TS och Vue, Prettier resten). I Rider/WebStorm:

- _Languages & Frameworks → JavaScript → Code Quality Tools → ESLint_: "Automatic ESLint configuration" och "Run eslint --fix on save"
- _Languages & Frameworks → JavaScript → Prettier_: "Automatic Prettier configuration", "Run on save" och "Run for files" satt till `**/*.{json,md,yml,yaml,css,html}`. Standardmönstret gäller bara JS, TS och Vue, alltså tvärtom mot vad vi vill.
