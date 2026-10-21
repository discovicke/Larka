# net25-devops-grupp-4

## Köra lokalt

```bash
npm install            # i repots rot, en gång (husky + concurrently)
npm --prefix client install
npm run dev
```

`npm run dev` startar backend (http://localhost:5257) och Vite (http://localhost:5173) samtidigt. Öppna Vite-adressen. Vite skickar `/api` och `/health` vidare till backend, så webbläsaren pratar bara med en origin och ingen CORS behövs.

- API:et ligger under `/api`, t.ex. `/api/groups`. `/health` ligger på roten.
- Swagger finns på http://localhost:5257/swagger.

## Publicera

```bash
dotnet publish backend -c Release -o publish
```

Publish bygger klienten (`npm ci` + `npm run build`) och lägger den i `wwwroot`, så backend serverar både API:et och klienten. Sökvägar som inte är filer eller `/api` får klientens `index.html`.

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
# Backend
dotnet format RebornLMS.sln --verify-no-changes
dotnet build RebornLMS.sln
dotnet test RebornLMS.sln

# Frontend
cd client
npm run format:check
npm run lint
npm run build
npm run test
```

`npm run format` och `dotnet format RebornLMS.sln` fixar formateringen automatiskt.
