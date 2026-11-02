# Konfiguration

Alla inställningar för backend, var de sätts och vilka som är hemligheter.

## Så läses inställningarna

Backend läser i ordning, där det sista vinner:

1. `appsettings.json`
2. `appsettings.Development.json` (bara lokalt, `ASPNETCORE_ENVIRONMENT=Development`)
3. user-secrets (bara lokalt)
4. miljövariabler (Render, Docker)

En nyckel skrivs med `:` i JSON och user-secrets och med `__` som miljövariabel: `Auth:Jwt:Key` blir `Auth__Jwt__Key`.

**Hemligheter hamnar aldrig i `appsettings`.** Lokalt ligger de i user-secrets, i prod i GitHub-miljön `production`, som deploy-jobbet skickar vidare till Render (se [deploy.md](deploy.md#hemligheter)).

```bash
dotnet user-secrets set <Nyckel> "<värde>" --project backend
dotnet user-secrets list --project backend
```

## Backend

### Databas

| Nyckel                      | Hemlig | Standard                      | Vad                                                                                                                                                                         |
| --------------------------- | ------ | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default` | ja\*   | Docker-Postgres i Development | Connection string i .NET-format. I prod Supabase session pooler (IPv4).                                                                                                     |
| `Database:SeedDevData`      | nej    | `true` i Development          | Lägger in exempeldata om databasen saknar grupper.                                                                                                                          |
| `Database:SeedDemoData`     | nej    | `true` i Production           | Lägger in realistisk demodata för Edugrade vid start, en gång. Se [auth-och-behorigheter.md](auth-och-behorigheter.md#demodata-prod). Satt i `appsettings.Production.json`. |

\* Lokalt är standardvärdet bara dev-lösenordet till Docker-containern.

### Inloggning

| Nyckel                     | Hemlig | Standard                 | Vad                                         |
| -------------------------- | ------ | ------------------------ | ------------------------------------------- |
| `Auth:Jwt:Key`             | ja     | dev-nyckel i Development | Minst 32 byte. Appen startar inte utan den. |
| `Auth:Jwt:LifetimeMinutes` | nej    | `480`                    | Hur länge ett token gäller.                 |
| `Auth:Jwt:Issuer`          | nej    | `larka`                  | Ändras inte i praktiken.                    |
| `Auth:Jwt:Audience`        | nej    | `larka`                  | Ändras inte i praktiken.                    |

### Fillagring

| Nyckel                            | Hemlig | Standard | Vad                             |
| --------------------------------- | ------ | -------- | ------------------------------- |
| `Storage:Supabase:Url`            | nej    | tom      | `https://<projekt>.supabase.co` |
| `Storage:Supabase:ServiceRoleKey` | ja     | tom      | Går förbi row level security.   |
| `Storage:Supabase:Bucket`         | nej    | `files`  | En privat bucket.               |

Saknas `Url` eller `ServiceRoleKey` lagras filerna i minnet i Development, och i andra miljöer felar första filanropet. Se [fillagring.md](fillagring.md).

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

| Namn                        | Typ      | Var                 | Används av                            |
| --------------------------- | -------- | ------------------- | ------------------------------------- |
| `RENDER_API_KEY`            | secret   | miljön `production` | deploy: Render API                    |
| `RENDER_SERVICE_ID`         | secret   | miljön `production` | deploy: vilken tjänst                 |
| `APP_URL`                   | variabel | miljön `production` | deploy: kontroll av `/version`        |
| `AUTH_JWT_KEY`              | secret   | miljön `production` | → `Auth__Jwt__Key` på Render          |
| `SUPABASE_URL`              | variabel | miljön `production` | → `Storage__Supabase__Url`            |
| `SUPABASE_SERVICE_ROLE_KEY` | secret   | miljön `production` | → `Storage__Supabase__ServiceRoleKey` |
| `GH_APP_ID`                 | variabel | miljön `production` | → `GitHub__AppId` (valfri)            |
| `GH_APP_SLUG`               | variabel | miljön `production` | → `GitHub__AppSlug` (valfri)          |
| `GH_CLIENT_ID`              | variabel | miljön `production` | → `GitHub__ClientId` (valfri)         |
| `GH_ORG`                    | variabel | miljön `production` | → `GitHub__Org` (valfri)              |
| `GH_INSTALLATION_ID`        | variabel | miljön `production` | → `GitHub__InstallationId` (valfri)   |
| `GH_APP_PRIVATE_KEY`        | secret   | miljön `production` | → `GitHub__PrivateKey` (valfri)       |
| `GH_CLIENT_SECRET`          | secret   | miljön `production` | → `GitHub__ClientSecret` (valfri)     |
| `GH_WEBHOOK_SECRET`         | secret   | miljön `production` | → `GitHub__WebhookSecret` (valfri)    |
| `E2E_BASE_URL`              | variabel | repot               | E2E-workflowen (valfri)               |

`GITHUB_TOKEN` kommer från Actions och används för semantic-release och för att pusha imagen till GHCR.

Connection string till prod-databasen sätts bara av [`infra/render-setup.sh`](../infra/render-setup.sh), inte av deploy-jobbet.
