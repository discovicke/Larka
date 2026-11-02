# Deploy och releaser

## Flödet

Allt som når `main` deployas automatiskt av [`cd.yml`](../.github/workflows/cd.yml):

1. **Tests**: samma kontroller som på PR:er ([ci.yml](../.github/workflows/ci.yml))
2. **Release: version and tag**: semantic-release sätter versionstagg (se [Versioner](#versioner))
3. **Docker image**: bygger Docker-imagen och pushar den till `ghcr.io/eduedugrade/net25-devops-grupp-4`, taggad med commit, version och `latest`
4. **Deploy to Render**: [`render-deploy.sh`](../.github/scripts/render-deploy.sh) skickar appens inställningar till Render, säger åt Render att köra imagen, väntar tills den är live och kontrollerar att `/version` svarar med rätt commit

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

## Inställningar och hemligheter

Appens inställningar för prod ligger i GitHub-miljön `production`, under appens egna nycklar (`AUTH__JWT__KEY`, `CONNECTIONSTRINGS__DEFAULT`, `AUTH__ENTRA__TENANTID`). Vid varje deploy skickar [`render-deploy.sh`](../.github/scripts/render-deploy.sh) **alla** variabler och secrets med `__` i namnet till Render, en i taget, så att annat på tjänsten lämnas orört. En ny inställning läggs alltså bara i miljön. Finns samma nyckel på Render med andra versaler (`Auth__Jwt__Key`) tas den gamla stavningen bort, så att .NET inte får två värden.

Saknas `CONNECTIONSTRINGS__DEFAULT` eller `AUTH__JWT__KEY` stoppar deployen med ett fel innan något ändras. `RENDER_API_KEY`, `RENDER_SERVICE_ID` och `APP_URL` är deployens egna och skickas inte till appen. Hela listan: [konfiguration.md](konfiguration.md#github-actions).

## En ny skola

En skola har en egen installation: eget repo, egen Supabase-databas och egen Render-tjänst. Så kommer den igång:

1. Sätt upp databasen och värdtjänsten. Med vår uppsättning: ett Supabase-projekt och Render-tjänsten ([`infra/render-setup.sh`](../infra/render-setup.sh)), och inställningarna i miljön `production` (se [Inställningar och hemligheter](#inställningar-och-hemligheter)). Någon annanstans: [drift.md](drift.md). Sätt inte `DATABASE__SEEDDEMODATA`.
2. Kör [`infra/entra-setup.sh`](#microsoft-inloggning) inloggad i skolans tenant. Du som kör det blir skolans första admin.
3. Pusha till `main` (eller kör CD igen) så att deployen sätter värdena på Render.
4. Logga in med Microsoft. Skolan kopplas till tenanten, får organisationens namn, och du blir admin.

Sedan loggar alla andra in med Microsoft och blir elever, i klassen som adressen slutar på om den finns. Admin gör lärare och mentorer under _Användare_.

En ny installation har inga konton med kända lösenord och ingen demodata. Demokontona (`admin`, `larare`, `elev`) och Edugrades demodata finns bara när `Database__SeedDemoData` är `true`, som i vår demo, och lokalt i Development.

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

Sist skrivs `Auth__Entra__ClientId`, `TenantId`, `AdminObjectId` (ditt object id) och `SchoolName` (organisationens namn, eller `SCHOOL_NAME` om du sätter den) ut och sparas i miljön `production`, och nästa deploy skickar dem till Render. Klienten hämtar dem från API:et, så ingen ny build behövs. `SKIP_GITHUB=1` skriver bara ut raderna, färdiga att klistra in i en `.env` eller en molntjänsts inställningar ([drift.md](drift.md)).

Appregistreringen är ingen Azure-resurs och syns inte under _Resources_ i Azure-portalen. Den ligger i skolans Entra ID och kostar ingenting. Spärrar skolan Entra-portalen för vanliga användare fungerar skriptet ändå, eftersom det går via Microsoft Graph.

### Flytta till en ny tenant

Kör skriptet igen inloggad i den nya tenanten och deploya. Första gången du loggar in med Microsoft efter det byts skolans gamla tenant ut mot den nya, och du blir admin om du inte redan finns i skolan. Användare från den gamla tenanten kan inte logga in längre, men deras konton och data finns kvar.

### Vår demo

Demon på Render använder en egen testtenant (`christiangennari61gmail.onmicrosoft.com`), inte Edugrades. Edugrades Entra ID används inte. Miljön `production` har `DATABASE__SEEDDEMODATA=true` (demokontona och demodatan) och `AUTH__ENTRA__SCHOOLNAME=Edugrade`, så att demoskolan behåller namnet. Testkontona `elev.test.net25@…` (elev i .NET25) och `larare.test@…` finns i testtenanten. Lösenorden delas inte här, fråga Christian.

### Testa med en egen tenant

En gratis Microsoft-tenant räcker, till exempel katalogen som följer med ett personligt Azure-konto. Du är global admin där, så skriptet ger samtycke för hela organisationen. Skapa testanvändare med `az ad user create`. Har tenanten _security defaults_ på (standard för nya tenanter) måste varje användare registrera Microsoft Authenticator vid första inloggningen. För en testtenant kan du stänga av det under _Entra admin center › Översikt › Egenskaper › Hantera standardinställningar för säkerhet_. Gör inte det i en riktig skolas tenant.

## Versioner

Varje push till `main` kör [semantic-release](https://semantic-release.gitbook.io/), som läser commits sedan förra taggen och räknar fram nästa version enligt [semver](https://semver.org/):

| Commit på `main`                                      | Version       |
| ----------------------------------------------------- | ------------- |
| `fix: ...`                                            | patch 1.0.x   |
| `feat: ...`                                           | minor 1.x.0   |
| `feat!: ...` eller `BREAKING CHANGE:` i commit-texten | major x.0.0   |
| `docs`, `test`, `ci`, `chore` m.fl.                   | ingen release |

Den skapar en git-tagg (`v1.2.3`) och en GitHub-release med release notes. Inget commitas tillbaka till `main`, så branch-skyddet behöver inga undantag. Commit-formatet beskrivs i [bidra.md](bidra.md#commit-format).
