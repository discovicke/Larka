# Deploy och releaser

## Flödet

Allt som når `main` deployas automatiskt av [`cd.yml`](../.github/workflows/cd.yml):

1. **Tests**: samma kontroller som på PR:er ([ci.yml](../.github/workflows/ci.yml))
2. **Release: version and tag**: semantic-release sätter versionstagg (se [Versioner](#versioner))
3. **Docker image**: bygger Docker-imagen och pushar den till `ghcr.io/eduedugrade/net25-devops-grupp-4`, taggad med commit, version och `latest`
4. **Deploy to Render**: [`render-deploy.sh`](../.github/scripts/render-deploy.sh) sätter hemligheterna på Render, säger åt Render att köra imagen, väntar tills den är live och kontrollerar att `/version` svarar med rätt commit

## Kontrollerna på GitHub

Samma kontroller syns på PR:er och på `main`. På `main` kör CD först hela CI (därför heter de där `CD / Tests / …`) och skickar sedan ut versionen.

| Kontroll                                    | Var             | Vad den gör                                                                              | Obligatorisk på PR |
| ------------------------------------------- | --------------- | ---------------------------------------------------------------------------------------- | ------------------ |
| `CI / Detect changes`                       | PR, `main`      | Ser om bara dokumentation ändrats, då hoppas backend över och frontend kör bara Prettier | nej                |
| `CI / Backend: build, format, tests`        | PR, `main`      | Bygger utan varningar, kör `dotnet format` och alla backendtester                        | ja                 |
| `CI / Frontend: format, lint, build, tests` | PR, `main`      | Prettier, ESLint, att API-typerna är aktuella, bygger med typkontroll och enhetstester   | ja                 |
| `CI / Coverage report`                      | PR, `main`      | Slår ihop täckningen från backend och frontend (se [testing.md](testing.md))             | nej                |
| `PR title / Conventional commit title`      | PR              | PR-titeln ska följa Conventional Commits, eftersom den blir commiten som styr versionen  | ja                 |
| `CD / Update coverage badge`                | `main`          | Uppdaterar täckningsbadgen i README                                                      | –                  |
| `CD / Release: version and tag`             | `main`          | Räknar fram nästa version, taggar och skapar en GitHub-release                           | –                  |
| `CD / Docker image`                         | `main`          | Bygger och pushar imagen                                                                 | –                  |
| `CD / Deploy to Render`                     | `main`          | Deployar imagen och kontrollerar `/version`                                              | –                  |
| `E2E / Smoke tests against prod`            | schema, manuell | Playwright mot den deployade appen varje morgon (se [testing.md](testing.md))            | –                  |

De obligatoriska kontrollerna står i rulesetet `main` under _Settings › Rules_ med samma namn. Byter ett jobb namn måste rulesetet uppdateras samtidigt, annars väntar PR:er på en kontroll som aldrig rapporterar.

Render kör imagen i Frankfurt och databasen är Supabase Postgres i samma region. Misslyckas en deploy behåller Render den gamla versionen live. `GET /version` visar vilken version och commit som körs.

Gratisinstansen somnar efter 15 minuter utan trafik. Första anropet efter det kan ta en minut.

## Render-tjänsten

Render har ingen åtkomst till repot. Tjänsten kör imagen från GHCR med en token som bara får läsa paket, och deployer triggas av GitHub Actions, inte av Render.

Tjänsten skapas och konfigureras med [`infra/render-setup.sh`](../infra/render-setup.sh). Kör den lokalt när tjänstens inställningar ändras. Skriptet beskriver själv vilka variabler det behöver.

> Körs skriptet mot en befintlig tjänst uppdateras en variabel i taget. Variabler som bara satts för hand på Render, till exempel GitHub-nycklar, finns kvar.

## Hemligheter

Hemligheterna finns i GitHub-miljön `production`, och deploy-jobbet skickar dem till Render vid varje deploy, en variabel i taget så att övriga variabler på tjänsten lämnas orörda:

| I `production`              | På Render                                                      |
| --------------------------- | -------------------------------------------------------------- |
| `AUTH_JWT_KEY`              | `Auth__Jwt__Key`                                               |
| `SUPABASE_URL`              | `Storage__Supabase__Url`                                       |
| `SUPABASE_SERVICE_ROLE_KEY` | `Storage__Supabase__ServiceRoleKey`                            |
| `ENTRA_CLIENT_ID`           | `Auth__Entra__ClientId` (bara om båda Entra-variablerna finns) |
| `ENTRA_TENANT_ID`           | `Auth__Entra__TenantId`                                        |
| `ENTRA_ADMIN_OBJECT_ID`     | `Auth__Entra__AdminObjectId` (om den finns)                    |
| `ENTRA_SCHOOL_NAME`         | `Auth__Entra__SchoolName` (om den finns)                       |
| `SEED_DEMO_DATA`            | `Database__SeedDemoData`, alltid (`false` när den saknas)      |

GitHub är valfritt. Finns värdena nedan skickas de också vid varje deploy, en variabel i taget. Saknas de deployar appen utan GitHub-integration.

| I `production`       | På Render                |
| -------------------- | ------------------------ |
| `GH_APP_ID`          | `GitHub__AppId`          |
| `GH_APP_SLUG`        | `GitHub__AppSlug`        |
| `GH_CLIENT_ID`       | `GitHub__ClientId`       |
| `GH_ORG`             | `GitHub__Org`            |
| `GH_INSTALLATION_ID` | `GitHub__InstallationId` |
| `GH_APP_PRIVATE_KEY` | `GitHub__PrivateKey`     |
| `GH_CLIENT_SECRET`   | `GitHub__ClientSecret`   |
| `GH_WEBHOOK_SECRET`  | `GitHub__WebhookSecret`  |

`RENDER_API_KEY`, `RENDER_SERVICE_ID` och `APP_URL` används bara av deploy-jobbet. Alla finns i [konfiguration.md](konfiguration.md#github-actions). Saknas någon av de obligatoriska värdena stoppar deploy-jobbet med ett fel innan något ändras.

## En ny skola

En skola har en egen installation: eget repo, egen Supabase-databas och egen Render-tjänst. Så kommer den igång:

1. Skapa Supabase-projektet och Render-tjänsten ([`infra/render-setup.sh`](../infra/render-setup.sh)) och lägg hemligheterna i miljön `production` (se [Hemligheter](#hemligheter)). Sätt inte `SEED_DEMO_DATA`.
2. Kör [`infra/entra-setup.sh`](#microsoft-inloggning) inloggad i skolans tenant. Du som kör det blir skolans första admin.
3. Pusha till `main` (eller kör CD igen) så att deployen sätter värdena på Render.
4. Logga in med Microsoft. Skolan kopplas till tenanten, får organisationens namn, och du blir admin.

Sedan loggar alla andra in med Microsoft och blir elever, i klassen som adressen slutar på om den finns. Admin gör lärare och mentorer under _Användare_.

En ny installation har inga konton med kända lösenord och ingen demodata. Demokontona (`admin`, `larare`, `elev`) och Edugrades demodata finns bara när `SEED_DEMO_DATA` är `true`, som i vår demo, och lokalt i Development.

## Microsoft-inloggning

Skolans Entra-admin, eller någon i skolan om skolan låter användare registrera appar, sätter upp inloggningen med Microsoft en gång, med [`infra/entra-setup.sh`](../infra/entra-setup.sh):

```bash
az login          # i skolans tenant
gh auth login     # med rätt att ändra miljön production
infra/entra-setup.sh
```

Skriptet registrerar Lärka i skolans Entra ID, eller uppdaterar registreringen om den redan finns (den hittas på taggen `larka-sign-in`):

- bara konton i skolans egen tenant kan logga in (single-tenant)
- plattformen _Single-page application_ med redirect URI:erna `APP_URL/login` och `http://localhost:5173/login`
- bara inloggningsbehörigheterna `openid`, `profile`, `email` och `offline_access`, och ingen hemlighet
- samtycke för hela organisationen om den som kör är admin. Annars godkänner varje användare själv vid första inloggningen, vilket skolans standardregler brukar tillåta för just inloggning

Sist sparas `ENTRA_CLIENT_ID`, `ENTRA_TENANT_ID`, `ENTRA_ADMIN_OBJECT_ID` (ditt object id) och `ENTRA_SCHOOL_NAME` (organisationens namn, eller `SCHOOL_NAME` om du sätter den) i miljön `production`, och nästa deploy sätter dem på Render. Klienten hämtar dem från API:et, så ingen ny build behövs. `SKIP_GITHUB=1` skriver bara ut värdena.

Appregistreringen är ingen Azure-resurs och syns inte under _Resources_ i Azure-portalen. Den ligger i skolans Entra ID och kostar ingenting. Spärrar skolan Entra-portalen för vanliga användare fungerar skriptet ändå, eftersom det går via Microsoft Graph.

## Versioner

Varje push till `main` kör [semantic-release](https://semantic-release.gitbook.io/), som läser commits sedan förra taggen och räknar fram nästa version enligt [semver](https://semver.org/):

| Commit på `main`                                      | Version       |
| ----------------------------------------------------- | ------------- |
| `fix: ...`                                            | patch 1.0.x   |
| `feat: ...`                                           | minor 1.x.0   |
| `feat!: ...` eller `BREAKING CHANGE:` i commit-texten | major x.0.0   |
| `docs`, `test`, `ci`, `chore` m.fl.                   | ingen release |

Den skapar en git-tagg (`v1.2.3`) och en GitHub-release med release notes. Inget commitas tillbaka till `main`, så branch-skyddet behöver inga undantag. Commit-formatet beskrivs i [bidra.md](bidra.md#commit-format).
