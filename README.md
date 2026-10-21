# net25-devops-grupp-4

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
