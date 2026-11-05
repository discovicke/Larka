# Konfiguration

Alla inställningar för backend, var de sätts och vilka som är hemligheter.

## Så läses inställningarna

Backend läser `appsettings.json`, sedan `appsettings.Development.json` (bara lokalt) och sist miljövariabler, där det sista vinner. `Auth:Jwt:Key` i JSON heter `Auth__Jwt__Key` som miljövariabel.

- **`appsettings.json`:** hur appen beter sig, samma överallt (jobbens intervall, tokenets livslängd).
- **Miljövariabler:** det som skiljer mellan installationer, och alla hemligheter. [`deploy/.env.example`](../deploy/.env.example) listar dem.

Var miljövariablerna kommer ifrån:

| Var               | Källa                                                                                                                                    |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Lokalt            | Inget behövs. Valfria hemligheter: `infisical run -- dotnet run --project backend` (miljön `dev`)                                        |
| Vår prod (Render) | Infisical, projektet Lärka, miljön `prod`. Deployen hämtar dem och ersätter Renders inställningar ([deploy.md](deploy.md#inställningar)) |
| En skolas server  | `deploy/.env` ([drift.md](drift.md))                                                                                                     |

## Backend

### Databas

| Nyckel                      | Hemlig | Standard                      | Vad                                                                                                               |
| --------------------------- | ------ | ----------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Default` | ja\*   | Docker-Postgres i Development | Connection string i .NET-format. I prod Supabase session pooler (IPv4).                                           |
| `Database:SeedDemoData`     | nej    | `true` i Development          | Lägger in demokonton och kompakt demodata. Se [auth-och-behorigheter.md](auth-och-behorigheter.md#demodata-prod). |
| `Database:DemoTemplateRepo` | nej    | tom                           | Mallrepot (`ägare/namn`) för demodatans GitHub-uppgift. Tom: första mallrepot i skolans kopplade organisation.    |

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
| `Auth:FirstAdmin:Email`    | nej    | tom                      | Första admin för en skola utan Microsoft. Skapas vid start om skolan saknar admin.       |
| `Auth:FirstAdmin:Password` | ja     | tom                      | Minst 8 tecken. Ett fel värde stoppar starten.                                           |
| `Auth:FirstAdmin:Name`     | nej    | tom                      | Namnet som visas. Tomt betyder att det byggs ur e-postadressen.                          |

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

Jobbet skapar notiser när en publiceringstid har passerat och påminnelser 24 timmar före en deadline. Påminnelsen gäller elevens nästa deadline som återstår (huvuddeadline, kompletteringsdeadline eller elevens egen) och går till elever som är Inte inlämnad eller Komplettera, en gång per elev och deadline. Uppgifter som bara bedöms på plats och elever som stängt av påminnelserna hoppas över. Det som redan har en notis hoppas över, så jobbet kan köras hur ofta som helst. Det är avstängt i testerna.

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

| Variabel       | Var                  | Standard                | Vad                                                                                |
| -------------- | -------------------- | ----------------------- | ---------------------------------------------------------------------------------- |
| `BACKEND_URL`  | Vite (`npm run dev`) | `http://localhost:5257` | Vart Vite skickar `/api`, `/health`, `/version`.                                   |
| `E2E_BASE_URL` | `e2e/`               | `http://localhost:8080` | Stacken som E2E-testerna kör mot. En adress på `onrender.com` vägras.              |
| `E2E_APP_PORT` | `e2e/`               | `8080`                  | Värdporten för E2E-stacken (`docker-compose.e2e.yml`) när 8080 är upptagen lokalt. |

E2E loggar in som demokontot `admin` och skapar sina egna lärare och elever, se [testing.md](testing.md#e2e-tre-användarflöden).

## GitHub Actions

Workflows har inga egna hemligheter. Deployen loggar in i Infisical med GitHubs OIDC-token. `GITHUB_TOKEN` används för semantic-release och för att pusha imagen till GHCR. E2E-workflowet bygger sin egen stack och behöver inga variabler.
