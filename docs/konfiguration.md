# Konfiguration

Alla inställningar för backend, var de sätts och vilka som är hemligheter.

## Så läses inställningarna

Backend läser i ordning, där det sista vinner:

1. `appsettings.json`
2. `appsettings.Development.json` (bara lokalt, `ASPNETCORE_ENVIRONMENT=Development`)
3. user-secrets (bara lokalt)
4. miljövariabler (Render, Docker, `.env`)

En nyckel skrivs med `:` i JSON och user-secrets och med `__` som miljövariabel: `Auth:Jwt:Key` blir `Auth__Jwt__Key`. Versaler spelar ingen roll.

### Var värdena hör hemma

| Sort                                  | Var                        | Exempel                               |
| ------------------------------------- | -------------------------- | ------------------------------------- |
| Hur appen beter sig, samma överallt   | `appsettings.json` i repot | jobbens intervall, tokenets livslängd |
| Det som skiljer mellan installationer | miljön där appen körs      | connection string, tenant, fillagring |
| Hemligheter                           | miljön, aldrig i repot     | JWT-nyckeln, databasens lösenord      |

[`deploy/.env.example`](../deploy/.env.example) är listan över det en installation ställer in. Den används som den är på en egen server (`deploy/.env`), och samma nycklar läggs i värdtjänstens inställningar i molnet ([drift.md](drift.md)). Lokalt ligger hemligheterna i user-secrets, och i vår prod i GitHub-miljön `production` (se [GitHub Actions](#github-actions)).

```bash
dotnet user-secrets set <Nyckel> "<värde>" --project backend
dotnet user-secrets list --project backend
```

## Backend

### Databas

| Nyckel                      | Hemlig | Standard                      | Vad                                                                                                                                                                |
| --------------------------- | ------ | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ConnectionStrings:Default` | ja\*   | Docker-Postgres i Development | Connection string i .NET-format. I prod Supabase session pooler (IPv4).                                                                                            |
| `Database:SeedDevData`      | nej    | `true` i Development          | Lägger in exempeldata om databasen saknar grupper.                                                                                                                 |
| `Database:SeedDemoData`     | nej    | `false`                       | Lägger in demokontona och realistisk demodata för Edugrade vid start. Se [auth-och-behorigheter.md](auth-och-behorigheter.md#demodata-prod). Sätts bara i en demo. |

\* Lokalt är standardvärdet bara dev-lösenordet till Docker-containern.

### Inloggning

| Nyckel                     | Hemlig | Standard                 | Vad                                                                                      |
| -------------------------- | ------ | ------------------------ | ---------------------------------------------------------------------------------------- |
| `Auth:Jwt:Key`             | ja     | dev-nyckel i Development | Minst 32 byte. Appen startar inte utan den.                                              |
| `Auth:Jwt:LifetimeMinutes` | nej    | `480`                    | Hur länge ett token gäller.                                                              |
| `Auth:Jwt:Issuer`          | nej    | `larka`                  | Ändras inte i praktiken.                                                                 |
| `Auth:Jwt:Audience`        | nej    | `larka`                  | Ändras inte i praktiken.                                                                 |
| `Auth:Entra:ClientId`      | nej    | tom                      | Lärkas app i skolans Entra ID. Utan den och `TenantId` finns ingen Microsoft-inloggning. |
| `Auth:Entra:TenantId`      | nej    | tom                      | Skolans tenant. Bara konton därifrån kan logga in med Microsoft.                         |
| `Auth:Entra:AdminObjectId` | nej    | tom                      | Den som körde `entra-setup.sh`. Hens första inloggning kopplar skolan och gör hen admin. |
| `Auth:Entra:SchoolName`    | nej    | tom                      | Organisationens namn. Skolan får det när den kopplas på det sättet.                      |

Ingen av Entra-nycklarna är hemlig. [`infra/entra-setup.sh`](../infra/entra-setup.sh) skapar appen och sparar värdena, se [deploy.md](deploy.md#microsoft-inloggning).

### Fillagring

| Nyckel                            | Hemlig | Standard | Vad                                                              |
| --------------------------------- | ------ | -------- | ---------------------------------------------------------------- |
| `Storage:Supabase:Url`            | nej    | tom      | `https://<projekt>.supabase.co`                                  |
| `Storage:Supabase:ServiceRoleKey` | ja     | tom      | Går förbi row level security.                                    |
| `Storage:Supabase:Bucket`         | nej    | `files`  | En privat bucket.                                                |
| `Storage:Disk:Path`               | nej    | tom      | Mapp för filerna när Supabase inte används, t.ex. `/data/files`. |

Supabase används när `Url` och `ServiceRoleKey` finns, annars disk när `Storage:Disk:Path` finns. Utan någon av dem lagras filerna i minnet i Development, och i andra miljöer felar första filanropet. Se [fillagring.md](fillagring.md).

### Notiser

| Nyckel                              | Hemlig | Standard | Vad                                                                 |
| ----------------------------------- | ------ | -------- | ------------------------------------------------------------------- |
| `Notifications:Job:Enabled`         | nej    | `true`   | Bakgrundsjobbet som skapar notiser för publiceringar och deadlines. |
| `Notifications:Job:IntervalMinutes` | nej    | `5`      | Hur ofta jobbet körs.                                               |

Jobbet skapar notiser när en publiceringstid har passerat och påminnelser 24 timmar före en deadline. Det som redan har en notis hoppas över, så jobbet kan köras hur ofta som helst. Det är avstängt i testerna.

### Körmiljö

| Miljövariabel               | Var           | Vad                                                              |
| --------------------------- | ------------- | ---------------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`    | Render        | `Production`. Lokalt sätter `launchSettings.json` `Development`. |
| `PORT`                      | Render        | `8080`, porten som aspnet-imagen lyssnar på.                     |
| `APP_VERSION`, `GIT_COMMIT` | Docker-imagen | Bakas in vid bygget och visas av `GET /version`.                 |

### GitHub

GitHub-appen är gemensam för plattformen. Varje skola har en installation och en organisation.
Se [github-integration.md](github-integration.md) för hur flödena hänger ihop.

| Nyckel                               | Hemlig | Vad                                                                             |
| ------------------------------------ | ------ | ------------------------------------------------------------------------------- |
| `GitHub:AppId`                       | nej    | GitHub Appens ID.                                                               |
| `GitHub:AppSlug`                     | nej    | Appens slug, används i installationslänken.                                     |
| `GitHub:PrivateKey`                  | ja     | PEM eller base64-kodad PEM.                                                     |
| `GitHub:PrivateKeyPath`              | ja     | Alternativ filsökväg. Används om `PrivateKey` saknas.                           |
| `GitHub:ClientId`                    | nej    | OAuth client id för elevens GitHub-koppling.                                    |
| `GitHub:ClientSecret`                | ja     | OAuth client secret.                                                            |
| `GitHub:WebhookSecret`               | ja     | Secret för webhook-signaturer när webhooken är aktiverad.                       |
| `GitHub:ApiBaseUrl`                  | nej    | Standard `https://api.github.com/`.                                             |
| `GitHub:ProvisioningJob:*`           | nej    | Bakgrundsjobb för att skapa elevrepon. Standard är aktiverat.                   |
| `GitHub:DeadlineLockJob:*`           | nej    | Bakgrundsjobb för deadline-låsning. Standard är aktiverat.                      |
| `GitHub:ChecksJob:*`                 | nej    | Fallback-poll för commits och checks. Standard är aktiverat var 6:e timme.      |
| `GitHub:Support:NotStartedAfterDays` | nej    | Antal dagar innan en elev utan commits räknas som "inte startat". Standard `3`. |
| `GitHub:Support:NoPushAfterDays`     | nej    | Antal dagar utan push innan en påbörjad uppgift flaggas. Standard `4`.          |
| `GitHub:Support:RedChecksAfterHours` | nej    | Antal timmar med röda tester innan de flaggas. Standard `36`.                   |

`GitHub:InstallationId` och `GitHub:Org` finns kvar som lokal dev-fallback, men en rad i
`SchoolGitHubInstallations` vinner alltid.

GitHub-webhooken pekar på:

```text
https://<backend>/api/webhooks/github
```

Aktivera `check_run`, `workflow_run` och `push`. Signaturen verifieras med `GitHub:WebhookSecret`.
Om webhooken är avstängd uppdaterar `GitHub:ChecksJob` elevrepon med jämna mellanrum i stället.

Elevens egen "Jag behöver hjälp"-flagga skickar en notis till lärarna på uppgiften. Automatiska
signaler visas bara i lärartrackingen och skickar inga notiser.

#### Säkerhetskrav i GitHub-organisationen

Elevrepon ägs av organisationen. Eleven läggs till som collaborator med `push`, inte som ägare.
Det gör att skolan behåller kontrollen och att GitHub Actions, org-policyer och appens åtkomst fungerar.

För att elever bara ska se sina egna repon och mallar som de har fått åtkomst till:

1. Sätt **Organization settings → Member privileges → Base permissions** till **No permission**.
   Med `Read` eller `Write` ser alla organisationens medlemmar alla privata repon.
2. Stäng av **Allow members to create repositories** om eleverna inte ska kunna skapa egna repon.
3. Stäng av **Allow members to delete repositories**.
4. Stäng av **Allow members to change repository visibility**.
5. Kräv tvåfaktorsautentisering för organisationen.
6. Lägg inte elever i team som har åtkomst till andra elevrepon. Team får användas för lärare eller
   grupprepon, aldrig för att ge hela klassen åtkomst till individuella elevrepon.

GitHub Appens installation har fortfarande åtkomst till organisationens repon och kan skapa,
arkivera och hantera elevrepon. Mallrepon ska vara privata och markerade som **Template repository**.

## Klient och tester

| Variabel                       | Var                   | Standard                                  | Vad                                              |
| ------------------------------ | --------------------- | ----------------------------------------- | ------------------------------------------------ |
| `BACKEND_URL`                  | Vite (`npm run dev`)  | `http://localhost:5257`                   | Vart Vite skickar `/api`, `/health`, `/version`. |
| `E2E_BASE_URL`                 | `e2e/`, repo-variabel | https://net25-devops-grupp-4.onrender.com | Appen som E2E-testerna kör mot.                  |
| `E2E_USERNAME`, `E2E_PASSWORD` | `e2e/`                | `elev` / `elev`                           | Kontot som E2E loggar in med.                    |

## GitHub Actions

Miljön `production` håller allt deployen behöver. Två sorters namn:

- **Appens inställningar** heter som appens nycklar, med `__`: `AUTH__JWT__KEY`, `CONNECTIONSTRINGS__DEFAULT`, `AUTH__ENTRA__TENANTID` (GitHub sparar namnen med versaler). Hemligheter som secrets, resten som variabler. Deployen skickar **alla** namn med `__` till Render, så en ny inställning läggs bara i miljön, inget skript ändras. Lägg därför inget annat med `__` i miljön. GitHub tillåter inga namn som börjar på `GITHUB_`, så GitHub-integrationens nycklar får prefixet `LARKA_`, som tas bort: `LARKA_GITHUB__APPID` blir `GitHub:AppId`.
- **Deployens egna** saknar `__` och skickas inte vidare:

| Namn                | Typ      | Var                 | Används av                     |
| ------------------- | -------- | ------------------- | ------------------------------ |
| `RENDER_API_KEY`    | secret   | miljön `production` | deploy: Render API             |
| `RENDER_SERVICE_ID` | secret   | miljön `production` | deploy: vilken tjänst          |
| `APP_URL`           | variabel | miljön `production` | deploy: kontroll av `/version` |
| `E2E_BASE_URL`      | variabel | repot               | E2E-workflowen (valfri)        |

`CONNECTIONSTRINGS__DEFAULT` och `AUTH__JWT__KEY` måste finnas, annars stoppar deployen innan något ändras. I vår prod finns också `STORAGE__SUPABASE__URL`, `STORAGE__SUPABASE__SERVICEROLEKEY`, `AUTH__ENTRA__*` och `DATABASE__SEEDDEMODATA=true`. GitHub-integrationens nycklar ligger som `LARKA_GITHUB__*`.

`GITHUB_TOKEN` kommer från Actions och används för semantic-release och för att pusha imagen till GHCR.
