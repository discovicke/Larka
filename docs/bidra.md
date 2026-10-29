# Bidra

Kör `npm install` i repots rot en gång efter kloning. Det aktiverar git-hooken som kontrollerar commit-meddelanden.

## Arbetsflöde

Varje ändring har en issue, en branch och en PR som stänger issuen. `main` är skyddad: en PR mergas först när alla kontroller är gröna och branchen är uppdaterad mot `main`. PR:er squash-mergas.

## Commit-format

Vi använder [conventional commits](https://www.conventionalcommits.org/). Commits och PR-titlar ska se ut så här:

```
<typ>(<valfritt scope>): <kort beskrivning>
```

Vanliga typer: `feat` (ny funktion), `fix` (buggfix), `docs`, `test`, `refactor`, `style`, `build`, `ci`, `chore`.

Exempel: `feat(client): add course page`, `fix(backend): return 404 for missing course`.

PR-titeln blir commiten på `main` och kontrolleras i CI. Typen avgör nästa version, se [deploy.md](deploy.md#versioner).

## Kontroller på varje PR

CI kör följande och alla måste vara gröna. Kör dem lokalt innan du pushar:

```bash
# Formatering i hela repot (YAML, JSON, Markdown, CSS, HTML)
npx prettier --check .

# Backend
dotnet build backend.Analyzers   # våra egna regler, måste finnas innan dotnet format körs
dotnet format Larka.sln --verify-no-changes
dotnet build Larka.sln       # skriver också client/openapi.json
dotnet test Larka.sln

# Frontend
cd client
npm run gen:api                  # API-typerna från client/openapi.json
npm run lint
npm run build
npm run test
npm run test:layout
```

`npm run format` i repots rot fixar formateringen i hela repot (Prettier, ESLint och `dotnet format`), och `npm run format:check` kontrollerar den.

## Kodstil

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

groups.map((group) => group.id) // inga enbokstavsnamn, utom i i loopar, T och _
```

```vue
<Button/>                      <!-- inget mellanslag före /> -->
<input type="text"/>
```

I C# gäller även regler som `dotnet format` inte har själv: max 120 tecken per rad, `?` och `:` i en ternary på egna rader, inga namn på en bokstav och inga property patterns. Se [code-style-csharp.md](code-style-csharp.md).

## Editorn

Ställ in editorn så att den formaterar med samma verktyg som CI.

**VS Code:** installera tilläggen ESLint, Prettier och C# Dev Kit, och slå på _Format on Save_ (ESLint fixar JS, TS och Vue, Prettier resten).

**Rider/WebStorm:**

- _Languages & Frameworks → JavaScript → Code Quality Tools → ESLint_: "Automatic ESLint configuration" och "Run eslint --fix on save"
- _Languages & Frameworks → JavaScript → Prettier_: "Automatic Prettier configuration", "Run on save" och "Run for files" satt till `**/*.{json,md,yml,yaml,css,html}`. Standardmönstret gäller bara JS, TS och Vue, alltså tvärtom mot vad vi vill.
