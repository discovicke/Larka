# Inloggning och behörigheter

## Inloggning

Inloggning med lösenord och JWT är alltid på. Allt under `/api` kräver ett token, utom `/api/auth/*`. `/health`, `/version` och Swagger UI är öppna. Klienten skickar tokenet som `Authorization: Bearer` och skickar användaren till `/login` när det saknas eller har gått ut.

Inloggningen tar användarnamn eller e-post och lösenord, och jämför e-posten utan hänsyn till versaler. Alla fel ger samma svar, `401` med texten `Fel e-post eller lösenord.`.

Vem som är inloggad ligger bakom `ICurrentUser`, som läser användaren från tokenet. Tokenet gäller i 8 timmar (`Auth:Jwt:LifetimeMinutes`).

**JWT-nyckeln** (`Auth:Jwt:Key`) är en hemlighet på minst 32 byte, och appen stoppar vid start om den saknas. I Development finns en dev-nyckel i `appsettings.Development.json`. Var den sätts i övrigt: [konfiguration.md](konfiguration.md#inloggning).

### Inloggning med Microsoft

Skolans elever och personal loggar in med sitt Microsoft-konto (Microsoft Entra ID). Ingen kan skapa ett konto på något annat sätt, och ingen väljer själv sin roll eller klass. Inställningen görs en gång med [`infra/entra-setup.sh`](../infra/entra-setup.sh) ([deploy.md](deploy.md#microsoft-inloggning)).

1. `GET /api/auth/providers` berättar om Microsoft-inloggning finns (`microsoft: {clientId, tenantId}` eller `null`). Klienten loggar in hos Microsoft med MSAL (redirect, auth code med PKCE, `openid profile email`) och får ett ID-token.
2. Klienten skickar det en gång till `POST /api/auth/microsoft` `{idToken}`. Backend kontrollerar signaturen mot tenantens nycklar, utfärdaren (skolans tenant), mottagaren (`Auth:Entra:ClientId`) och tiden, och svarar med samma token som inloggning med lösenord, plus `firstSignIn`. Resten av API:et ser ingen skillnad.
3. Användaren hittas på Microsofts id (`oid`, sparat som `User.ExternalId`). Saknas det kopplas en användare i skolan med samma e-post (utan hänsyn till versaler) som inte redan är kopplad, till exempel en elev från en inklistrad elevlista. Annars skapas en ny **elev**.
4. Namnet är Microsofts `name`. Saknas det, eller är det bara e-postadressen, byggs namnet ur adressen (`SchoolAddress.NameFromEmail`): `christian.gennari.net25@...` ger `Christian Gennari` och `anna-karin.berg.net25@...` ger `Anna-Karin Berg`. Adressen delas på punkt och understreck, klasskoden sist tas bort och varje del (och varje del av ett bindestreck) får stor första bokstav. Ger adressen inget namn används själva adressen. Eleven kan rätta namnet på _Välkommen_.
5. En ny elev läggs i klassen som adressen slutar på: `fornamn.efternamn.net25@...` ger `net25`, som matchar klassen `.NET25` (versaler, punkter och mellanslag räknas inte; samma regel i `SchoolAddress.NormalizeCode`). Bara om exakt en klass i skolan har namnet, och bara när användaren skapas. Annars har eleven ingen klass förrän admin lägger in hen. Dialogerna _Ny klass_ och _Redigera klass_ visar vilken kod namnet fångar ("Elever med adresser som slutar på `.net25@…` hamnar här", regeln speglas i `client/src/lib/classCode.ts`) och varnar när namnet inte ger någon kod, till exempel "NET 25 DevOps". En kopplad användare behåller sin roll och sina klasser.

På `/login` står _Logga in med Microsoft_ överst och lösenordet under (för demokontona). Microsoft skickar tillbaka till `/login` (redirect URI:n), där klienten tar emot svaret och öppnar sidan användaren ville till. Första gången öppnas _Välkommen_ (`/valkommen`) i stället: namnet från Microsoft (eller ur adressen) kan rättas (`PUT /api/me/profile` `{name}`) och notiserna väljas. En elev utan klass och kurser ser _Du är inte med i någon klass än_ på Idag. _Logga ut_ loggar bara ut ur Lärka, inte ur Microsoft. MSAL laddas först när någon loggar in med Microsoft.

En ny skola kopplas av den som körde `entra-setup.sh` (`Auth:Entra:AdminObjectId`): när ingen skola har tenanten än och hen loggar in med Microsoft kopplas skolan från migrationen till tenanten, får organisationens namn (`Auth:Entra:SchoolName`), och hen skapas som **admin**. Pekar skolan på en gammal tenant (appen har flyttats till en ny tenant) byts den ut på samma sätt. Ingen annan kan koppla skolan så, och alla andra får `403` tills den är kopplad. `GET /api/auth/providers` skickar också skolans namn (`schoolName`), som inloggningssidan visar.

Annars tar skolan emot Microsoft-inloggningar först när admin har slagit på det under _Inställningar → Integrationer → Inloggning med Microsoft_. Då kopplas appens tenant till skolan (`School.EntraTenantId`, en tenant per skola). `GET /api/schools/{id}/microsoft` visar läget (`configured`, `linked`, `tenantId`), `PUT` kopplar (`404` när `Auth:Entra` saknas, `409` när en annan skola redan har tenanten) och `DELETE` kopplar loss. Bara admin i skolan.

På _Användare_ visar kolumnen _Microsoft_ vem som har loggat in med Microsoft. _Koppla bort Microsoft_ i menyn `⋯` (`DELETE /api/users/{id}/microsoft`) tömmer `ExternalId`, till exempel när skolans IT har skapat om någons konto. Nästa inloggning kopplar det nya kontot på e-postadressen. Admins _Idag_ listar **Elever utan klass**, till exempel de som loggade in första gången utan att adressen matchade en klass.

| Svar från `POST /api/auth/microsoft` | När                                                                                                    |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| `200`                                | Inloggad. `firstSignIn` är sant första gången.                                                         |
| `401`                                | Tokenet är ogiltigt: fel signatur, tenant, mottagare eller för gammalt.                                |
| `403`                                | Skolan har inte kopplat Microsoft, kontot är spärrat, eller e-posten används redan av ett annat konto. |
| `404`                                | Microsoft-inloggning är inte inställd (`Auth:Entra` saknas).                                           |

## Konton

### Demokonton (Development och demon)

Tre konton läggs in vid varje start i Development och när `Database:SeedDemoData` är `true` (Edugrades demo), och deras lösenord sätts om vid varje start. En ny skolas installation har dem inte, se [deploy.md](deploy.md#en-ny-skola):

| Användarnamn | Lösenord | Roll    |
| ------------ | -------- | ------- |
| `admin`      | `admin`  | Admin   |
| `larare`     | `larare` | Teacher |
| `elev`       | `elev`   | Student |

Vid start skapas också klassen `Demo-klass` (läraren och eleven i den), och om skolan saknar kurser kursen `Demo-kurs` (kod `DEMO`) med en vecka. Klassen läser bara `Demo-kurs`, aldrig skolans övriga kurser. Ingen annan användare rörs. Kontona är offentliga, så prod-data är öppen för den som prövar dem.

När demodatan nedan finns skapas varken `Demo-klass` eller `Demo-kurs`, och kontona sitter i de riktiga klasserna i stället.

### Demodata (prod)

Prod fylls med realistisk data för skolan **Edugrade**, så att varje funktion går att visa. Det gör `DemoSeed` (`backend/Infrastructure/Persistence/Seeding/DemoSeed.cs`, innehållet i `DemoCatalog.cs`) vid start, när `Database:SeedDemoData` är `true`. Inställningen sätts i Infisical (`prod`), och deployen skickar den till Render. Den är av som standard, i Development och i testerna, så en ny skola får ingen demodata. Se [konfiguration.md](konfiguration.md#databas).

Seeden är additiv och idempotent. Finns klassen `.NET25` (markören) gör den ingenting, och den ändrar eller tar aldrig bort något den inte skapat själv. Undantagen: skolan döps om från `Placeholder-skolan` till `Edugrade`, kontona `larare` och `elev` placeras i `.NET25`, och `Demo-klass` med `Demo-kurs` tas bort om ingen har lämnat in något där (annars lämnas de). Allt sparas i en transaktion. Ett fel loggas men stoppar inte starten.

Eftersom seeden hoppar över allt när `.NET25` finns når ändringar i `DemoCatalog` aldrig en databas som redan har demodata. Därför kör `DemoContentRefresh` direkt efter seeden vid varje start, en gång per version: konstanten `DemoContentRefresh.ContentVersion` (höj den när katalogen ändrats) sparas i tabellen `DataPatches` när uppdateringen gått igenom. Uppdateringen går igenom katalogen och matchar veckor, delar och nyheter på titel, eller på den gamla titeln i `Was` om något döpts om. Den ändrar bara texten (kursers titel och beskrivning, veckors titel och beskrivning, delars och nyheters titel och brödtext), skapar det som saknas (delar utan inlämningar, nyheter och kommentarer) och lämnar allt annat orört: datum, skalor, inlämningar, betyg, klarmarkeringar, lärarnas egna delar och befintliga kommentarer. Den har samma flagga, egen transaktion och samma regel att ett fel loggas men inte stoppar starten.

| Grupp             | Typ       | Innehåll                                                                                                                                                                                                                                    |
| ----------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.NET25` (HT25)   | Klass     | Systemutvecklare .NET med AI-kompetens. 22 elever, 4 lärare (`larare` och tre till), 1 mentor. Avslutade kurser: Programmering med C#, Databaser, Webbapplikationer i C#. Pågående: DevOps. Kommande: AI-komponenter och ML i Azure, LIA 1. |
| `.NET24` (HT24)   | Klass     | Äldre klass som är ute på LIA. 19 elever, 3 lärare, 1 mentor. Fyra avslutade kurser (C#, Databaser, Webbapplikationer, DevOps), AI-kursen, och pågående LIA 2 med veckologg.                                                                |
| `FIB25` (HT25)    | Klass     | Fibertekniker, utan kod och GitHub. 18 elever, 3 lärare, 1 mentor. Skarvning och mätning (avslutad), Passiva fibernät och installation (pågående), Fältpraktik (kommande). Praktiska prov bedöms på plats.                                  |
| `Klassråd .NET25` | Committee | Visar grupptyper: tre elever, `elev`, `larare` och mentorn. Har en nyhet med kommentarer men inga kurser.                                                                                                                                   |

DevOps (`.NET25_DEVOPS`, 12 oktober till 13 november, 25 poäng) har veckorna v42–v46 med tre lektioner vardera (Git-flöden, CI med GitHub Actions, Docker, tester, semantisk versionering och driftsättning), en läsuppgift och fem inlämningsuppgifter. Deadlines och lägen kring demodagen, måndag 2 november:

| Uppgift                      | Deadline | Läge                                                                                                                                   |
| ---------------------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Git-flöde i teamet           | 23 okt   | Nästan alla betygsatta (någon IG), kompletteringen 30 okt har gått ut: någon har Chanserna slut                                        |
| CI-pipeline för er backend   | 28 okt   | Många väntar på bedömning (kön i _Att bedöma_, några Inlämnat igen), några ska komplettera, några saknas. Kompletteringsdeadline 6 nov |
| Containerisera applikationen | 30 okt   | Försenad: bara en del har lämnat in, ingen kompletteringsdeadline                                                                      |
| Testautomation i CI          | 6 nov    | Ska in i veckan (veckan öppnar fredag 30 okt)                                                                                          |
| Releasa och driftsätt        | 13 nov   | Längre fram (veckan öppnar fredag 6 nov)                                                                                               |

Alla inlämningar och betyg är daterade senast 29 oktober 2026, så inget ligger i framtiden. Demodatan följer betygsreglerna: Komplettera och Ej inlämnat finns bara där en kompletteringsdeadline återstår (CI-pipeline, 6 nov) eller har gått ut (då väntar IG), och varje flik i _Att bedöma_ har minst en rad. Dessutom finns nyheter i klasserna och kurserna (välkomst, schemaändring, påminnelse före deadline) med kommentarer, klarmarkerade lektioner, beroenden mellan lektioner (en låst del) och inkorg för `elev` och `larare`. Avslutade kurser har betyg på varje uppgift för varje elev (G eller VG). Det finns inga GitHub-repon eller installationer i demodatan.

**Logga in som, för att visa:**

| Konto                         | Lösenord     | Visar                                                                                                                                                                                                                                                                                                                            |
| ----------------------------- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `admin`                       | `admin`      | Skolans ordning: Idag med vad som behöver ordnas (kurser utan lärare, klasser utan mentor, användare utan lösenord), alla klasser, användare och kurser, lärare på kurser och skolinställningarna. Läser kursernas innehåll, men undervisar inte: inga inlämningar, inget _Att bedöma_ och ingen redigering av veckor och delar. |
| `larare`                      | `larare`     | Lärare i `.NET25`: Idag med _Att bedöma_-kö, deadlines och schemalagda veckor, kursflikarna i DevOps och Webbapplikationer i C#, klasslistor och bedömning.                                                                                                                                                                      |
| `elev`                        | `elev`       | Elev i `.NET25` med historik: Idag, Kalender, Inkorg, Material, Studieplan och betyg (95 av 225 poäng klara), inlämningar som är betygsatta, väntar och ska kompletteras.                                                                                                                                                        |
| `malin.ekstrom@demo.larka.se` | `larka-demo` | Mentor för `.NET25`: ser klassens kurser (under _Klassens kurser_), publicerat innehåll och elevernas inlämningar och betyg som läsning, med _Visa_ i stället för _Bedöm_. Kan inte bedöma eller publicera nyheter.                                                                                                              |
| Valfri seedad användare       | `larka-demo` | Alla lärare, mentorer och elever har e-post `fornamn.efternamn@demo.larka.se` (utan å, ä, ö) och samma lösenord. Till exempel `karin.holmgren@demo.larka.se` (lärare), `malin.ekstrom@demo.larka.se` (mentor), `mikael.strand@demo.larka.se` (lärare i Fibertekniker).                                                           |

För att visa en annan klass: logga in som en lärare eller elev där, till exempel `henrik.lindahl@demo.larka.se` (mentor för `.NET24`) eller `sofie.brannstrom@demo.larka.se` (mentor för `FIB25`). Seedade användare har samma lösenord, och det är medvetet offentligt, så logga inte in med riktiga uppgifter där.

### Dev-inloggningar

Dev-seeden (tom lokal databas) ger alla seedade användare lösenordet `dev-password`:

| Användare        | E-post                    | Id                                     | Roll    |
| ---------------- | ------------------------- | -------------------------------------- | ------- |
| Placeholder      | `placeholder@larka.local` | `00000000-0000-0000-0000-000000000001` | Admin   |
| Lärare Lärarsson | `larare@larka.local`      | `00000000-0000-0000-0000-000000000002` | Teacher |
| Elev Elevsson    | `elev@larka.local`        | `00000000-0000-0000-0000-000000000003` | Student |
| Elev Tvåsson     | `elev2@larka.local`       | `00000000-0000-0000-0000-000000000004` | Student |

```bash
curl -X POST http://localhost:5257/api/auth/login -H "Content-Type: application/json" \
  -d '{"username":"larare@larka.local","password":"dev-password"}'
curl -H "Authorization: Bearer <accessToken>" http://localhost:5257/api/me
```

Seeden körs bara när databasen är tom. Saknar en äldre dev-databas de här användarna, börja om: `docker compose down -v && npm run db`.

### Produktion

Prod seedar inga lösenord utöver demokontona och demodatan (`larka-demo`). Admin kan ge en ny användare ett tillfälligt lösenord (minst 8 tecken) när hen skapas från _Lägg till medlem_ på en grupp, eller med `password` i `POST /api/users`. När Microsoft-inloggning är inställd visar _Lägg till medlem_ inget lösenordsfält, eftersom användaren loggar in med skolans konto. Utan lösenord kan användaren inte logga in. För äldre användare utan lösenord: hasha det med ASP.NET Core `PasswordHasher<User>` och skriv hashen i `Users.PasswordHash`.

## Roller och behörigheter

API:et avgör vad en användare får göra. Klienten hämtar rollerna från `GET /api/me` och kan dölja knappar och sidor, men det är bara för att göra gränssnittet tydligare: den som anropar API:et direkt möts av samma regler.

| Roll                         | Får                                                                                                                                                                                                                                                                                                                                                                     |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Admin (`User.Role`)          | Ordnar skolan: skapar och ändrar skolor, användare, grupper, medlemskap och gruppens kurser, och sätter lärare på kurser. Ändrar kursens titel, datum och poäng och tar bort kursen (inte om det finns elevarbete i den). Läser kursernas innehåll, med utkast. Undervisar inte: ingen redigering av veckor och delar, ingen beskrivning, inga inlämningar eller betyg. |
| Lärare                       | Skapar innehåll, men bara den som är lärare i minst en kurs (`CourseTeacher`) eller har lärarrollen i en grupp. Ändrar, publicerar och tar bort innehåll som är länkat till en kurs hen undervisar i: beskrivning, veckor, moduler, länkar, bilagor, nyheter och betyg på inlämningar.                                                                                  |
| Mentor (`GroupRole.Mentor`)  | Läser klassens kurser (publicerat innehåll), klasslistan och inlämningarna och betygen för eleverna i sin klass. Bedömer inte och publicerar inte nyheter.                                                                                                                                                                                                              |
| Elev                         | Läsa flöden, moduler och innehåll, lämna in, kommentera och klarmarkera delar.                                                                                                                                                                                                                                                                                          |
| Lärare i en annan kurs/grupp | Får 403 på det som hör till kursen eller gruppen hen inte undervisar, även när kursen läses av hens egen klass.                                                                                                                                                                                                                                                         |

Det som gäller en kurs avgörs av relationen till kursen, inte av den globala rollen. En admin som också undervisar blir lärare i kursen på samma sätt som alla andra, med en rad i `CourseTeacher`, och får då lärarens rättigheter i just den kursen. En användare med `User.Role` Lärare men utan kurs och utan lärarroll i en grupp får inte skapa innehåll. Samma gäller mentorn.

### Användare och medlemslistor

E-post och global roll (`User.Role`) lämnas bara ut till admin, och bara för användare i adminens egen skola. Alla andra får bara namn: nyheter, kommentarer och inlämningar bär författarens eller elevens namn (`authorName`, `studentName`), så klienten behöver inte slå upp användare.

| Anrop                          | Admin                                   | Medlem i gruppen (elev, lärare eller mentor) | Övriga i skolan | Användare i en annan skola                    |
| ------------------------------ | --------------------------------------- | -------------------------------------------- | --------------- | --------------------------------------------- |
| `GET /api/users`               | Skolans användare, med e-post och roll  | `403`                                        | `403`           | Admin ser sin egen skola, andra får `403`     |
| `GET /api/users/{id}`          | Användare i skolan, med e-post och roll | `403`                                        | `403`           | `404` för admin (en annan skola), andra `403` |
| `GET /api/groups/{id}/members` | Hela listan                             | Hela listan                                  | `403`           | `404`                                         |

### Skolroll

Admin ändrar en användares skolroll (`User.Role`: Elev, Lärare eller Admin) på sidan _Användare_ (`/users`, bara admin), med _Ändra roll_ i menyn `⋯` på raden. Sidan listar skolans användare med namn, e-post och roll, med sökning och flikar per roll.

| Anrop                                  | Admin                                   | Övriga |
| -------------------------------------- | --------------------------------------- | ------ |
| `PUT /api/users/{id}/role` `{role}`    | `200` med användaren                    | `403`  |
| Användare i en annan skola eller okänd | `404`                                   | `403`  |
| Den egna rollen                        | `409` `you cannot change your own role` | `403`  |

Ingen kan ändra sin egen roll. Den som ändrar är alltid admin i skolan, så skolan har alltid kvar minst en admin. Blir någon elev tas hen bort som lärare i alla kurser (raderna i `CourseTeacher`), eftersom bara lärare och admin kan vara lärare i en kurs. Det hen har skapat, bedömt och skrivit ligger kvar. Rollerna i grupperna (`GroupRole`) ändras inte, de sätts på gruppens sida. Blir en lärare admin behåller hen sina kurser.

### Spärra användare

Användare tas aldrig bort, eftersom deras inlämningar, betyg och kommentarer ska finnas kvar. Admin spärrar dem i stället, med _Spärra_ i menyn `⋯` på _Användare_, och släpper spärren med _Återaktivera_. Båda frågar först.

| Anrop                             | Admin                                  | Övriga |
| --------------------------------- | -------------------------------------- | ------ |
| `POST /api/users/{id}/deactivate` | `200`, sätter `deactivatedAt`          | `403`  |
| `POST /api/users/{id}/reactivate` | `200`, tar bort `deactivatedAt`        | `403`  |
| Sig själv                         | `409` `you cannot deactivate yourself` | `403`  |
| En annan skolas användare         | `404`                                  | `403`  |

En spärrad användare som loggar in med rätt lösenord får `403` med texten `Ditt konto är spärrat.` (fel lösenord ger som vanligt `401`, så spärren avslöjas inte). En inloggning som redan finns gäller tills tokenet går ut (8 timmar). Spärrade användare syns inte i gruppernas medlemslistor eller i listorna där admin väljer lärare, mentor och elever, men medlemskapen ligger kvar och namnet står kvar på det de har gjort. På _Användare_ har de märket _Spärrad_ och en egen flik.

`POST /api/groups/{id}/members/import` tar en inklistrad elevlista (`{lines: [{name, email}]}`, högst 500 rader) och är bara för admin (`403` för alla andra, `404` för en annan skolas grupp). Nya elever skapas utan lösenord, alla läggs till som elever i klassen i ett enda sparsteg, och svaret redovisar varje rad: `Created`, `Added`, `AlreadyMember`, `NotStudent`, `Duplicate` eller `Invalid`. Lärare och admin läggs inte till på det här sättet.

E-postadresser kontrolleras med en enda regel på servern (`EmailRule`), samma som klientens förhandsvisning av inklistrade listor: något före och efter `@` och en punkt i domänen, utan mellanslag. `POST /api/users` svarar `400` för en adress som inte klarar den, och importen markerar raden som `Invalid`.

Bara en användare vars skolroll är Lärare kan vara mentor eller lärare i en klass: `POST /api/groups/{id}/members` och `PUT /api/groups/{id}/members/{userId}` svarar `400` när admin eller en elev får rollen Mentor eller Lärare. Klassen har en mentor. Får någon rollen Mentor lämnar den förra klassen i samma steg, precis som i _Byt mentor_ på klassens sida, så ingen klass får två mentorer. I _Lägg till medlem_ finns därför inte Mentor som val när klassen redan har en, och där pekar texten på _Byt mentor_. Mentorer och lärare i klassen väljs bland lärare. Lärare i en kurs är däremot kvar som ovan: lärare eller admin, eftersom en admin som också undervisar står i `CourseTeacher`.

En admin som mot förmodan står som mentor läser ändå inga inlämningar eller betyg genom mentorvägen: `GET /api/submissions/{id}` ger `403`, som för alla andra admin utan lärarrad.

Lärare i en kurs som gruppen läser får också medlemslistan, utan att vara medlemmar i gruppen. Övriga i skolan får `403`.

Medlemslistan (`GroupMemberDto`) har bara id, namn och personens roll i just den gruppen, aldrig e-post eller global roll. En elev ser alltså klasskamraterna med namn och grupproll. Det finns ingen profilbild i datamodellen. Lärare, mentorer och admin får samma lista.

Lärarnas kontaktuppgifter (namn, e-post och vilka grupper de undervisar) visas via kursens egen lärarlista, `GET /api/courses/{id}/staff`, och inte via `/api/users`. Listan är bara för dem som hör till kursen: kursens lärare, eleverna och mentorn i klasserna som läser den, och admin i kursens skola. Alla andra, också lärare och elever i andra kurser och admin i en annan skola, får `404`.

Admin hanterar klasser, grupper och kurser direkt på _Alla grupper_: knappen _Ny_ uppe till höger skapar en klass eller grupp och öppnar dess sida, där kurser skapas, och menyn `⋯` på varje rad har _Redigera_ och _Ta bort_. Borttagningen frågar alltid först och namnger det som tas bort. Nya användare skapas från _Lägg till medlem_ på gruppens sida ("Finns inte i listan? Skapa användare"). Den gamla sidan _Program och klasser_ under Inställningar finns inte kvar, och `/settings/groups` skickar vidare till _Alla grupper_.

### Lärare i en kurs

Klassen har elever och en mentor, och kursen har sina lärare. En lärare i en kurs är den som står i `CourseTeacher` för kursen. Det avgör vem som får redigera kursen, dess veckor och innehåll, och bedöma inlämningar. Att vara lärare i en klass (`GroupRole.Teacher`) ger ingen behörighet i klassens kurser, så en lärare i `.NET25` som undervisar i DevOps kan inte ändra eller betygsätta Databaser. Läraren behöver inte vara medlem i klassen. Admin är inte lärare på något vis förrän hen står där, och admin kan läsa kursen men inte undervisa i den.

`GroupRole.Teacher` gäller bara det som riktas till enbart en grupp, utan kurs, till exempel en nyhet till klassen: där avgör fortfarande rollen i gruppen (`CanTeachGroupAsync`). Innehåll som är riktat till en grupp _och_ en kurs räknas som kursens.

Samma regel gäller överallt: `GET /api/me` (rollen per kurs), Idag-sidan för lärare, kursens lärarlista, kalendern, kurslistan, grupplistan och notiser för nytt innehåll. Kursens lärare får kursens nyheter och uppgifter som notiser även om hen inte är med i klassen.

| Anrop                                        | Admin                      | Övriga (lärare, elev) |
| -------------------------------------------- | -------------------------- | --------------------- |
| `GET /api/courses/{id}/teachers`             | Kursens lärare             | `403`                 |
| `POST /api/courses/{id}/teachers` `{userId}` | `201`, lägger till läraren | `403`                 |
| `DELETE /api/courses/{id}/teachers/{userId}` | `204`, tar bort läraren    | `403`                 |

Användaren ska finnas i kursens skola och vara lärare eller admin i systemet (`400` annars, och `409` om hen redan är lärare i kursen). En kurs i en annan skola svarar `404`, även för admin. `GET /api/courses/{id}/staff` visar kursens lärare, med e-post och namnen på klasserna som läser kursen, för dem som hör till kursen (se ovan).

Demodatan (`DemoCatalog`) anger lärarna per kurs. `larare` undervisar i DevOps och Webbapplikationer i C# i `.NET25`, men inte i klassens övriga kurser trots att hen är lärare i klassen. När `DemoContentRefresh.ContentVersion` höjs rättar uppdateringen lärarna i prod efter katalogen: den tar bara bort lärare ur katalogens personer som katalogen inte har i kursen, och lägger till de som saknas. Riktiga konton lämnas orörda.

### Admin, lärare, mentor och elev i kursen

Admin bestämmer vem som undervisar vad, och läraren bestämmer vad som händer i kursen. Tabellen visar vad som gäller i en kurs. "Admin" är en admin i kursens skola som inte också står som lärare i kursen. En admin i en annan skola får `404` eller `403`, som alla andra utanför kursen.

| Anrop                                                | Lärare i kursen              | Mentor i klassen          | Elev i klassen       | Admin                      | Övriga        |
| ---------------------------------------------------- | ---------------------------- | ------------------------- | -------------------- | -------------------------- | ------------- |
| `GET /api/courses/{id}/modules`                      | Allt, med antal inlämningar  | Publicerat och öppet      | Publicerat och öppet | Allt, utan antal           | `403`         |
| `GET /api/courses/{id}/content`                      | Läser                        | Läser                     | Läser                | Läser                      | `403`         |
| `GET /api/groups/{id}/content`                       | Läser (kursen gruppen läser) | Läser (är medlem)         | Läser (är medlem)    | Läser                      | `403`         |
| `GET /api/content/{id}` (publicerat, se nedan)       | Läser                        | Läser                     | Läser                | Läser                      | `404`         |
| `GET /api/content/{id}` (ett utkast)                 | Läser                        | `404`                     | `404`                | Läser                      | `404`         |
| `PUT /api/courses/{id}`                              | Allt, med beskrivningen      | `403`                     | `403`                | Titel, datum och poäng     | `403`         |
| `DELETE /api/courses/{id}`                           | `403`                        | `403`                     | `403`                | `204` eller `409`          | `403`         |
| `GET /api/links/{id}/submissions`                    | Alla elever                  | Eleverna i mentorns klass | Bara den egna        | `403`                      | Bara den egna |
| `GET /api/groups/{id}/students/{studentId}/overview` | `404`                        | Eleverna i mentorns klass | `404`                | Eleverna i skolans klasser | `404`         |
| `GET /api/submissions/{id}`, filerna och betyget     | Läser                        | Läser eleverna i klassen  | Läser den egna       | `403`                      | `404`         |
| `POST /api/submissions/{id}/grade`                   | Bedömer                      | `403`                     | `403`                | `403`                      | `403`         |
| `POST /api/content`, `/api/content/with-link`        | Skapar                       | `403`                     | `403`                | `403`                      | `403`         |
| `POST /api/courses/{id}/runs`                        | `403`                        | `403`                     | `403`                | Från vilken kurs som helst | `403`         |
| `POST /api/courses/{id}/modules/publish`             | Publicerar veckornas utkast  | `403`                     | `403`                | `403`                      | `403`         |
| `GET /api/me/admin-overview`                         | `403`                        | `403`                     | `403`                | Skolans översikt           | `403`         |

**Elevens läge i klassen (`GET /api/groups/{id}/students/{studentId}/overview`).** Mentorn i klassen och admin i skolan ser läget för en elev i klassen: per kurs klassen läser antal uppgifter att komplettera, försenade, inlämnade men inte bedömda, och betygen hittills. Det är bara läsning och bygger på studieplanen, begränsad till klassens kurser. Alla andra, också en lärare som bara undervisar i en av klassens kurser, får `404`, och det får också den som frågar efter någon som inte är elev i klassen.

**Läsa ett innehåll (`GET /api/content/{id}`, kommentarer och bilagor).** Att innehållet är publicerat räcker inte. Det läses bara av

- den som får ändra det (lärare i en kurs, eller i en grupp för länkar utan kurs, där en länk pekar), författaren har ingen genväg,
- elever och mentorer i en grupp som läser en kurs där en länk pekar på innehållet och länken syns för eleverna: innehållet är publicerat, länkens `PublishAt` har passerat och veckan (om länken ligger i en) har öppnat. En länk till en viss grupp räknas bara för den gruppen, och en länk bara till en grupp (en nyhet) för gruppens medlemmar. Det är samma datumregler som flödena och elevvyn (`ContentVisibility`),
- admin i skolan där innehållet skrevs (`ContentAccess.MayReadAsync`): admin läser kursernas innehåll, med utkast, men undervisar inte.

Alla andra, också inloggade i en annan kurs eller skola, får `404` som för innehåll som inte finns. Kontrollen är en enda fråga per innehåll (`IPermissions.CanReadPublishedContentAsync`), flödena och veckovyerna har egna frågor och gör ingen kontroll per rad. Att redigera kräver lärarrätt på en länk (`MayEditAsync`, `403` annars): den som skrev innehållet men inte längre undervisar i kursen kan inte ändra det. Nytt innehåll skapas med `POST /api/content/with-link`, som prövar lärarrätt på kursen eller gruppen där det länkas, inte författarskap. Ett innehåll utan länk kan därför bara läsas av admin i skolan.

I `GET /api/courses/{id}/student-modules` har en låst del (beroende som inte är uppfyllt) kvar titel och blockerare men `bodyMd` är `null`, så texten går inte att läsa i förväg.

Filerna i en inlämning nås med kortlivade länkar som skapas när inlämningen läses, så en admin som inte får läsa inlämningen får inga länkar. Nyheter till en grupp kräver lärarrollen i gruppen (`CanTeachGroupAsync`), vilket mentorn inte har.

Admin som också undervisar i kursen (en rad i `CourseTeacher`) får lärarens kolumn i just den kursen, utöver adminens. Lärarens mallar för repo-uppgifter (`GET /api/integrations/github/templates` och senaste commit) och förhandsvisningen av reponamn kräver samma sak som att skapa innehåll, så en admin som inte undervisar får `403` där. Integrationens inställningar är fortfarande adminens.

Mentorn är medlem i klassen med `GroupRole.Mentor`. Mentorn läser alltså klassens kurser, klasslistan och elevernas inlämningar, men skapar, publicerar och bedömer ingenting. Den globala rollen spelar ingen roll: de seedade mentorerna har `User.Role` Lärare och får ändå inte skapa innehåll.

`GET /api/me/admin-overview` ger adminens Idag: antal kurser per läge (kommande, pågår, avslutade, utan datum), kurser utan lärare, klasser utan mentor och användare som inte kan logga in (inget lösenord, och skolan har inte Microsoft-inloggning) och elever utan klass. Spärrade användare räknas inte. Varje rad bär id:t på klassen eller kursen där admin ordnar det.

Nekas anropet svarar API:et `403 Forbidden` som ProblemDetails med texten `Du har inte behörighet.`. Läsning och elevens egna handlingar är öppna, och en elev ser bara sina egna inlämningar. Undantaget är användarlistan och medlemslistorna ovan.
