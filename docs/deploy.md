# Deploy och releaser

## Flödet

Allt som når `main` deployas automatiskt av [`cd.yml`](../.github/workflows/cd.yml):

1. **Tests**: samma kontroller som på PR:er ([ci.yml](../.github/workflows/ci.yml))
2. **Release: version and tag**: semantic-release sätter versionstagg (se [Versioner](#versioner))
3. **Docker image**: bygger Docker-imagen och pushar den till `ghcr.io/eduedugrade/net25-devops-grupp-4`, taggad med commit, version och `latest`
4. **Deploy to Render**: [`render-deploy.sh`](../.github/scripts/render-deploy.sh) hämtar inställningarna från Infisical, skickar dem till Render, säger åt Render att köra imagen, väntar tills den är live och kontrollerar att `/version` svarar med rätt commit

## Kontrollerna på GitHub

Samma kontroller syns på PR:er och på `main`. På `main` kör CD först hela CI (därför heter de där `CD / Tests / …`) och skickar sedan ut versionen.

| Kontroll                                    | Var             | Vad den gör                                                                                            | Obligatorisk på PR |
| ------------------------------------------- | --------------- | ------------------------------------------------------------------------------------------------------ | ------------------ |
| `CI / Detect changes`                       | PR, `main`      | Ser om bara dokumentation ändrats, då hoppas backend över och frontend kör bara Prettier               | nej                |
| `CI / Backend: build, format, tests`        | PR, `main`      | Bygger utan varningar, kör `dotnet format` och alla backendtester                                      | ja                 |
| `CI / Frontend: format, lint, build, tests` | PR, `main`      | Prettier, ESLint, att API-typerna är aktuella, bygger med typkontroll och enhetstester                 | ja                 |
| `CI / Coverage report`                      | PR, `main`      | Slår ihop täckningen från backend och frontend (se [testing.md](testing.md))                           | nej                |
| `PR title / Conventional commit title`      | PR              | PR-titeln ska följa Conventional Commits, eftersom den blir commiten som styr versionen                | ja                 |
| `CD / Update coverage badge`                | `main`          | Uppdaterar täckningsbadgen i README                                                                    | –                  |
| `CD / Release: version and tag`             | `main`          | Räknar fram nästa version, taggar och skapar en GitHub-release                                         | –                  |
| `CD / Docker image`                         | `main`          | Bygger och pushar imagen                                                                               | –                  |
| `CD / Deploy to Render`                     | `main`          | Deployar imagen och kontrollerar `/version`                                                            | –                  |
| `E2E / User journeys against a fresh stack` | schema, manuell | Tre användarflöden mot en färsk Docker-stack varje natt, aldrig på PR:er (se [testing.md](testing.md)) | –                  |

De obligatoriska kontrollerna står i rulesetet `main` under _Settings › Rules_ med samma namn. Byter ett jobb namn måste rulesetet uppdateras samtidigt, annars väntar PR:er på en kontroll som aldrig rapporterar.

Render kör imagen i Frankfurt och databasen är Supabase Postgres i samma region. Misslyckas en deploy behåller Render den gamla versionen live. `GET /version` visar vilken version och commit som körs.

Gratisinstansen somnar efter 15 minuter utan trafik. Första anropet efter det kan ta en minut.

## Render-tjänsten

Render har ingen åtkomst till repot. Tjänsten kör imagen från GHCR med en token som bara får läsa paket, och deployer triggas av GitHub Actions, inte av Render.

Tjänsten skapas och konfigureras med [`infra/render-setup.sh`](../infra/render-setup.sh). Kör den lokalt när tjänstens inställningar ändras. Skriptet beskriver själv vilka variabler det behöver.

## Inställningar

Alla inställningar och hemligheter ligger i [Infisical](https://eu.infisical.com), projektet **Lärka**. Den som behöver dem får en inloggning där, och ingen skickar värden till någon annan.

- **`prod`, roten:** appens inställningar med appens egna nycklar (`ConnectionStrings__Default`, `Auth__Jwt__Key`, `Auth__Entra__TenantId` …).
- **`prod`, `/deploy`:** deployens egna (`RENDER_API_KEY`, `RENDER_SERVICE_ID`). De når aldrig appen.
- **`dev`:** valfria hemligheter för lokal utveckling.

Vid varje deploy loggar [`render-deploy.sh`](../.github/scripts/render-deploy.sh) in i Infisical med GitHubs OIDC-token (identiteten `github-actions-deploy`, som bara får läsa och bara från miljön `production` i det här repot). Den hämtar `prod` och **ersätter** Renders inställningar med exakt dem. Ändra ett värde i Infisical och kör CD, så är det ute. Saknas `ConnectionStrings__Default` eller `Auth__Jwt__Key` stoppar deployen innan något ändras.

Repot använder GitHubs oföränderliga OIDC-subject, så identiteten i Infisical måste ha subject `repo:EduEdugrade@227339915/net25-devops-grupp-4@1426964077:environment:production` (audience `https://github.com/EduEdugrade`). Med bara `repo:EduEdugrade/net25-devops-grupp-4:…` svarar Infisical 403.

## En ny skola

1. Databas och värdtjänst: med vår uppsättning ett Supabase-projekt och Render-tjänsten ([`infra/render-setup.sh`](../infra/render-setup.sh)), annars [drift.md](drift.md).
2. Microsoft: kör [`infra/entra-setup.sh`](#microsoft-inloggning) och lägg raderna bland inställningarna. Du som kör det blir skolans första admin.
3. Deploya och logga in med Microsoft. Skolan kopplas till tenanten, får organisationens namn, och du blir admin. Alla andra blir elever, i klassen som adressen slutar på om den finns.

En ny installation har inga konton med kända lösenord och ingen demodata. Demokontona och Edugrades demodata finns bara med `Database__SeedDemoData=true`, som i vår demo, och lokalt.

## Microsoft-inloggning

```bash
az login                                              # i skolans tenant
APP_URL=https://larka.skolan.se infra/entra-setup.sh
```

Skriptet registrerar Lärka i skolans Entra ID (eller uppdaterar registreringen, som hittas på taggen `larka-sign-in`): single-tenant, _Single-page application_ med redirect till `APP_URL/login` och `http://localhost:5173/login`, bara inloggningsbehörigheterna och ingen hemlighet. Den som kör är admin ger skriptet samtycke för hela organisationen. Sist skrivs fyra rader `Auth__Entra__…` ut.

Appregistreringen är ingen Azure-resurs och kostar ingenting. Skriptet går via Microsoft Graph, så det fungerar även när skolan spärrar Entra-portalen för vanliga användare. Flyttas appen till en ny tenant: kör skriptet där, byt raderna och deploya. Din första inloggning kopplar skolan till den nya tenanten.

**Vår demo** använder en egen testtenant (`christiangennari61gmail.onmicrosoft.com`), inte Edugrades, med `Database__SeedDemoData=true` och `Auth__Entra__SchoolName=Edugrade`. Testkontona `elev.test.net25@…` och `larare.test@…` finns där. Testtenanten har _security defaults_ avstängt så att testkontona slipper MFA, vilket man aldrig gör i en riktig skolas tenant.

## Versioner

Varje push till `main` kör [semantic-release](https://semantic-release.gitbook.io/), som läser commits sedan förra taggen och räknar fram nästa version enligt [semver](https://semver.org/):

| Commit på `main`                                      | Version       |
| ----------------------------------------------------- | ------------- |
| `fix: ...`                                            | patch 1.0.x   |
| `feat: ...`                                           | minor 1.x.0   |
| `feat!: ...` eller `BREAKING CHANGE:` i commit-texten | major x.0.0   |
| `docs`, `test`, `ci`, `chore` m.fl.                   | ingen release |

Den skapar en git-tagg (`v1.2.3`) och en GitHub-release med release notes. Inget commitas tillbaka till `main`, så branch-skyddet behöver inga undantag. Commit-formatet beskrivs i [bidra.md](bidra.md#commit-format).
