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

| Nyckel                      | Hemlig | Standard                      | Vad                                                                     |
| --------------------------- | ------ | ----------------------------- | ----------------------------------------------------------------------- |
| `ConnectionStrings:Default` | ja\*   | Docker-Postgres i Development | Connection string i .NET-format. I prod Supabase session pooler (IPv4). |
| `Database:SeedDevData`      | nej    | `true` i Development          | Lägger in exempeldata om databasen saknar grupper.                      |

\* Lokalt är standardvärdet bara dev-lösenordet till Docker-containern.

### Inloggning

| Nyckel                     | Hemlig | Standard                 | Vad                                         |
| -------------------------- | ------ | ------------------------ | ------------------------------------------- |
| `Auth:Jwt:Key`             | ja     | dev-nyckel i Development | Minst 32 byte. Appen startar inte utan den. |
| `Auth:Jwt:LifetimeMinutes` | nej    | `480`                    | Hur länge ett token gäller.                 |
| `Auth:Jwt:Issuer`          | nej    | `rebornlms`              | Ändras inte i praktiken.                    |
| `Auth:Jwt:Audience`        | nej    | `rebornlms`              | Ändras inte i praktiken.                    |

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

GitHub-integrationens inställningar (`GitHub:*`) dokumenteras separat.

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
| `E2E_BASE_URL`              | variabel | repot               | E2E-workflowen (valfri)               |

`GITHUB_TOKEN` kommer från Actions och används för semantic-release och för att pusha imagen till GHCR.

Connection string till prod-databasen sätts bara av [`infra/render-setup.sh`](../infra/render-setup.sh), inte av deploy-jobbet.
