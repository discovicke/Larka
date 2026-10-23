# net25-devops-grupp-4

[![CD](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml/badge.svg)](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml)

Live: https://net25-devops-grupp-4.onrender.com (gratisinstansen somnar efter 15 minuter utan trafik, första anropet kan ta en minut)

## Köra lokalt

```bash
npm install            # i repots rot, en gång (husky + concurrently)
npm --prefix client install
npm run dev
```

`npm run dev` startar backend (http://localhost:5257) och Vite (http://localhost:5173) samtidigt. Öppna Vite-adressen. Vite skickar `/api`, `/health` och `/version` vidare till backend, så webbläsaren pratar bara med en origin och ingen CORS behövs.

- API:et ligger under `/api`, t.ex. `/api/groups`. `/health` och `/version` ligger på roten.
- Anropa API:et från klienten med `api` i `client/src/lib/api.ts`, t.ex. `await api.get<Group[]>('/api/groups')`. Fel kastas som `ApiError` med HTTP-status.
- Swagger finns på http://localhost:5257/swagger.

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
dotnet format RebornLMS.sln --verify-no-changes
dotnet build RebornLMS.sln
dotnet test RebornLMS.sln

# Frontend
cd client
npm run lint
npm run build
npm run test
```

`npm run format` i repots rot fixar formateringen i hela repot (Prettier, ESLint och `dotnet format`), och `npm run format:check` kontrollerar den.

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
```

```vue
<Button/>                      <!-- inget mellanslag före /> -->
<input type="text"/>
```

Ställ in editorn så att den formaterar med samma verktyg som CI. I Rider/WebStorm:

- _Languages & Frameworks → JavaScript → Code Quality Tools → ESLint_: "Automatic ESLint configuration" och "Run eslint --fix on save"
- _Languages & Frameworks → JavaScript → Prettier_: "Automatic Prettier configuration", "Run on save" och "Run for files" satt till `**/*.{json,md,yml,yaml,css,html}`. Standardmönstret gäller bara JS, TS och Vue, alltså tvärtom mot vad vi vill.
