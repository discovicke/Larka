# Lärka

[![CD](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml/badge.svg)](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml)
![Coverage](https://github.com/EduEdugrade/net25-devops-grupp-4/raw/badges/coverage.svg)

Ett lärplattformssystem (LMS) för skolor. Lärare skapar innehåll (nyheter, teori och uppgifter) och publicerar det till grupper. Elever lämnar in uppgifter och får betyg, kommentarer och notiser. Admin hanterar skolor, användare, kurser och grupper.

**Live:** https://net25-devops-grupp-4.onrender.com. Logga in som `elev`/`elev`, `elev-fiber`/`elev-fiber` (yrkesklassen), `larare`/`larare` eller `admin`/`admin`. Gratisinstansen somnar efter 15 minuter utan trafik, så första anropet kan ta en minut.

| Del     | Teknik                                       | Mapp                                |
| ------- | -------------------------------------------- | ----------------------------------- |
| Backend | ASP.NET Core (.NET 10), EF Core, Postgres    | `backend/`                          |
| Klient  | Vue 3, Vite, TypeScript, shadcn-vue          | `client/`                           |
| Tester  | xUnit med Testcontainers, Vitest, Playwright | `backend.Tests/`, `client/`, `e2e/` |

## Kom igång

Du behöver [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js 24](https://nodejs.org/) och [Docker](https://www.docker.com/).

```bash
npm install                  # i repots rot: git-hooks och dev-skript
npm --prefix client install
dotnet tool restore          # dotnet-ef
npm run db                   # Postgres i Docker
npm run dev                  # backend och klient
```

Öppna http://localhost:5173 och logga in med `larare`/`larare`, `elev`/`elev`, `elev-fiber`/`elev-fiber` eller `admin`/`admin`. En tom databas får samma demodata som demon. Swagger UI finns på http://localhost:5257/swagger.

Innan du pushar: `npm run format`, `dotnet test Larka.sln` och `npm --prefix client run test`. Alla kontroller finns i [docs/bidra.md](docs/bidra.md).

## Dokumentation

| Dokument                                                     | Innehåll                                                    |
| ------------------------------------------------------------ | ----------------------------------------------------------- |
| [Utveckling](docs/utveckling.md)                             | Dev-servern, databas och migrationer, API-typer i klienten  |
| [Konfiguration](docs/konfiguration.md)                       | Alla inställningar, miljövariabler och hemligheter          |
| [Arkitektur](docs/arkitektur.md)                             | Lagren i backend, ett anrop från början till slut, klienten |
| [Inloggning och behörigheter](docs/auth-och-behorigheter.md) | JWT, konton, roller och vad de får göra                     |
| [Fillagring](docs/fillagring.md)                             | Bilagor och inlämningar i Supabase Storage                  |
| [GitHub-integrationen](docs/github-integration.md)           | Repo-uppgifter, elevrepon, tester och deadline-lås          |
| [Deploy och releaser](docs/deploy.md)                        | CD-flödet, Render, hemligheter och semantic versioning      |
| [En ny skola](docs/deploy.md#en-ny-skola)                    | Installera Lärka för en skola och logga in med Microsoft    |
| [Drift](docs/drift.md)                                       | Köra Lärka på en egen server eller i molnet                 |
| [Testning](docs/testing.md)                                  | Teststrategi, kodtäckning och E2E                           |
| [Bidra](docs/bidra.md)                                       | Arbetsflöde, commit-format, PR-kontroller och kodstil       |
| [Kodstil i C#](docs/code-style-csharp.md)                    | Våra egna analyzer-regler                                   |
| [ER-modell](docs/er-diagram.md)                              | Datamodellen                                                |
| [Ordlista](docs/ordlista.md)                                 | Vad orden i gränssnittet heter i koden                      |
| [Design](docs/design.md)                                     | Mockupen och designbesluten                                 |
| [Varumärke](docs/varumarke.md)                               | Namn, logo, ton, färg och typografi                         |
| [UI-mönster](docs/ui-monster.md)                             | Återkommande lösningar i gränssnittet                       |
| [Roller och flöden](docs/roller-och-floden.md)               | Vem som gör vad och skolans år                              |
| [Designhistorik](docs/design-historik.md)                    | Källor och beslut som ersatte något                         |
