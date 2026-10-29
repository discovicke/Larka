# Deploy och releaser

## Flödet

Allt som når `main` deployas automatiskt av [`cd.yml`](../.github/workflows/cd.yml):

1. **checks**: samma kontroller som på PR:er ([ci.yml](../.github/workflows/ci.yml))
2. **release**: semantic-release sätter versionstagg (se [Versioner](#versioner))
3. **image**: bygger Docker-imagen och pushar den till `ghcr.io/eduedugrade/net25-devops-grupp-4`, taggad med commit, version och `latest`
4. **deploy**: [`render-deploy.sh`](../.github/scripts/render-deploy.sh) sätter hemligheterna på Render, säger åt Render att köra imagen, väntar tills den är live och kontrollerar att `/version` svarar med rätt commit

Render kör imagen i Frankfurt och databasen är Supabase Postgres i samma region. Misslyckas en deploy behåller Render den gamla versionen live. `GET /version` visar vilken version och commit som körs.

Gratisinstansen somnar efter 15 minuter utan trafik. Första anropet efter det kan ta en minut.

## Render-tjänsten

Render har ingen åtkomst till repot. Tjänsten kör imagen från GHCR med en token som bara får läsa paket, och deployer triggas av GitHub Actions, inte av Render.

Tjänsten skapas och konfigureras med [`infra/render-setup.sh`](../infra/render-setup.sh). Kör den lokalt när tjänstens inställningar ändras. Skriptet beskriver själv vilka variabler det behöver.

> Körs skriptet mot en befintlig tjänst ersätts **alla** miljövariabler på tjänsten med skriptets lista. Variabler som bara satts för hand på Render försvinner.

## Hemligheter

Hemligheterna finns i GitHub-miljön `production`, och deploy-jobbet skickar dem till Render vid varje deploy, en variabel i taget så att övriga variabler på tjänsten lämnas orörda:

| I `production`              | På Render                           |
| --------------------------- | ----------------------------------- |
| `AUTH_JWT_KEY`              | `Auth__Jwt__Key`                    |
| `SUPABASE_URL`              | `Storage__Supabase__Url`            |
| `SUPABASE_SERVICE_ROLE_KEY` | `Storage__Supabase__ServiceRoleKey` |

`RENDER_API_KEY`, `RENDER_SERVICE_ID` och `APP_URL` används bara av deploy-jobbet. Alla finns i [konfiguration.md](konfiguration.md#github-actions). Saknas någon av dem stoppar deploy-jobbet med ett fel innan något ändras.

## Versioner

Varje push till `main` kör [semantic-release](https://semantic-release.gitbook.io/), som läser commits sedan förra taggen och räknar fram nästa version enligt [semver](https://semver.org/):

| Commit på `main`                                      | Version       |
| ----------------------------------------------------- | ------------- |
| `fix: ...`                                            | patch 1.0.x   |
| `feat: ...`                                           | minor 1.x.0   |
| `feat!: ...` eller `BREAKING CHANGE:` i commit-texten | major x.0.0   |
| `docs`, `test`, `ci`, `chore` m.fl.                   | ingen release |

Den skapar en git-tagg (`v1.2.3`) och en GitHub-release med release notes. Inget commitas tillbaka till `main`, så branch-skyddet behöver inga undantag. Commit-formatet beskrivs i [bidra.md](bidra.md#commit-format).
