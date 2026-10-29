# Lärka

[![CD](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml/badge.svg)](https://github.com/EduEdugrade/net25-devops-grupp-4/actions/workflows/cd.yml)
![Coverage](https://github.com/EduEdugrade/net25-devops-grupp-4/raw/badges/coverage.svg)

Ett lärplattformssystem (LMS) för skolor. Arbetsnamnet var RebornLMS, som lösningen och namnrymderna fortfarande heter. Lärare skapar innehåll (nyheter, teori och uppgifter) och publicerar det till grupper. Studenter lämnar in uppgifter och får betyg, kommentarer och notiser. Admin hanterar skolor, användare, kurser och grupper.

**Live:** https://net25-devops-grupp-4.onrender.com. Logga in som `elev`/`elev`, `larare`/`larare` eller `admin`/`admin`. Gratisinstansen somnar efter 15 minuter utan trafik, så första anropet kan ta en minut.

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

Öppna http://localhost:5173 och logga in med `larare@rebornlms.local` / `dev-password`. Swagger UI finns på http://localhost:5257/swagger.

Innan du pushar: `npm run format`, `dotnet test RebornLMS.sln` och `npm --prefix client run test`. Alla kontroller finns i [docs/bidra.md](docs/bidra.md).

## Dokumentation

| Dokument                                                     | Innehåll                                                    |
| ------------------------------------------------------------ | ----------------------------------------------------------- |
| [Utveckling](docs/utveckling.md)                             | Dev-servern, databas och migrationer, API-typer i klienten  |
| [Konfiguration](docs/konfiguration.md)                       | Alla inställningar, miljövariabler och hemligheter          |
| [Arkitektur](docs/arkitektur.md)                             | Lagren i backend, ett anrop från början till slut, klienten |
| [Inloggning och behörigheter](docs/auth-och-behorigheter.md) | JWT, konton, roller och vad de får göra                     |
| [Fillagring](docs/fillagring.md)                             | Bilagor och inlämningar i Supabase Storage                  |
| [Deploy och releaser](docs/deploy.md)                        | CD-flödet, Render, hemligheter och semantic versioning      |
| [Testning](docs/testing.md)                                  | Teststrategi, kodtäckning, layouttester och E2E             |
| [Bidra](docs/bidra.md)                                       | Arbetsflöde, commit-format, PR-kontroller och kodstil       |
| [Kodstil i C#](docs/code-style-csharp.md)                    | Våra egna analyzer-regler                                   |
| [ER-modell](docs/er-diagram.md)                              | Datamodellen                                                |
| [Design](docs/design.md)                                     | Mockupen och designbesluten                                 |
