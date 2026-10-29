# Inloggning och behörigheter

## Inloggning

Inloggning med lösenord och JWT är alltid på. Allt under `/api` kräver ett token, utom `/api/auth/*`. `/health`, `/version` och Swagger UI är öppna. Klienten skickar tokenet som `Authorization: Bearer` och skickar användaren till `/login` när det saknas eller har gått ut.

Inloggningen tar användarnamn eller e-post och lösenord, och jämför e-posten utan hänsyn till versaler. Alla fel ger samma svar, `401` med texten `Fel e-post eller lösenord.`.

Vem som är inloggad ligger bakom `ICurrentUser`, som läser användaren från tokenet. Tokenet gäller i 8 timmar (`Auth:Jwt:LifetimeMinutes`).

**JWT-nyckeln** (`Auth:Jwt:Key`) är en hemlighet på minst 32 byte, och appen stoppar vid start om den saknas. I Development finns en dev-nyckel i `appsettings.Development.json`. Var den sätts i övrigt: [konfiguration.md](konfiguration.md#inloggning).

Microsoft Entra ID (#79) kan läggas till senare som ett andra JWT-schema, utan att resten ändras.

## Konton

### Demokonton (alla miljöer)

Tre konton läggs in vid varje start, i alla miljöer, och deras lösenord sätts om vid varje start:

| Användarnamn | Lösenord | Roll    |
| ------------ | -------- | ------- |
| `admin`      | `admin`  | Admin   |
| `larare`     | `larare` | Teacher |
| `elev`       | `elev`   | Student |

Vid start skapas också klassen `Demo-klass` (läraren och eleven i den), och om skolan saknar kurser kursen `Demo-kurs` (kod `DEMO`) med en vecka. Ingen annan användare rörs. Kontona är offentliga, så prod-data är öppen för den som prövar dem.

### Dev-inloggningar

Dev-seeden (tom lokal databas) ger alla seedade användare lösenordet `dev-password`:

| Användare        | E-post                        | Id                                     | Roll    |
| ---------------- | ----------------------------- | -------------------------------------- | ------- |
| Placeholder      | `placeholder@rebornlms.local` | `00000000-0000-0000-0000-000000000001` | Admin   |
| Lärare Lärarsson | `larare@rebornlms.local`      | `00000000-0000-0000-0000-000000000002` | Teacher |
| Elev Elevsson    | `elev@rebornlms.local`        | `00000000-0000-0000-0000-000000000003` | Student |
| Elev Tvåsson     | `elev2@rebornlms.local`       | `00000000-0000-0000-0000-000000000004` | Student |

```bash
curl -X POST http://localhost:5257/api/auth/login -H "Content-Type: application/json" \
  -d '{"username":"larare@rebornlms.local","password":"dev-password"}'
curl -H "Authorization: Bearer <accessToken>" http://localhost:5257/api/me
```

Seeden körs bara när databasen är tom. Saknar en äldre dev-databas de här användarna, börja om: `docker compose down -v && npm run db`.

### Produktion

Prod seedar inga lösenord utöver demokontona. Användare som skapas senare saknar lösenord tills det finns ett admin-UI. Sätt det med ett engångskommando eller SQL: hasha det med ASP.NET Core `PasswordHasher<User>` och skriv hashen i `Users.PasswordHash`.

## Roller och behörigheter

API:et avgör vad en användare får göra. Klienten hämtar rollerna från `GET /api/me` och kan dölja knappar och sidor, men det är bara för att göra gränssnittet tydligare: den som anropar API:et direkt möts av samma regler.

| Roll                         | Får                                                                                                                                                                    |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Admin (`User.Role`)          | Allt, i hela skolan. Skapa och ändra skolor, användare, grupper, medlemskap, kurser och gruppens kurser.                                                               |
| Lärare                       | Skapa innehåll. Ändra, publicera och ta bort eget innehåll, och innehåll som är länkat till en kurs hen undervisar: moduler, länkar, bilagor och betyg på inlämningar. |
| Elev                         | Läsa flöden, moduler och innehåll, lämna in, kommentera och klarmarkera delar.                                                                                         |
| Lärare i en annan kurs/grupp | Får 403 på det som hör till kursen eller gruppen hen inte undervisar.                                                                                                  |

En lärare är den som har `GroupRole.Teacher` i en grupp som läser kursen (`GroupCourse`), oavsett om `User.Role` är Teacher eller Admin. Admin räknas som lärare på alla skolans kurser.

Nekas anropet svarar API:et `403 Forbidden` som ProblemDetails med texten `Du har inte behörighet.`. Läsning och elevens egna handlingar är öppna, och en elev ser bara sina egna inlämningar.
