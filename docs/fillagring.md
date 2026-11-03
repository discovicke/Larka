# Fillagring

Filer (bilagor och inlämningar) ligger bakom `IFileStorage`: i en privat bucket i Supabase Storage (vår prod) eller i en mapp på disk (när en skola kör Lärka själv, se [drift.md](drift.md)). Databasen sparar bara nyckeln (`content/{contentId}/{guid}`, `submissions/{submissionId}/{guid}`), och svaren innehåller en länk som gäller i 10 minuter och skapas när filen läses. Åtkomstkontrollerna görs i backend innan länken skapas.

## Uppladdning

Uppladdning sker med `multipart/form-data`:

- bilagor till innehåll: `POST /api/content/{id}/attachments` (fältet `file`)
- inlämningar: `POST /api/links/{id}/submissions` (fälten `bodyMd` och `files`)

Högst 20 MB per fil, 10 filer och 50 MB per anrop, och bara filtyper som finns i `FileUploadPolicy` (pdf, bilder, txt, md, Office-filer och zip). Typen sätts från filändelsen, inte från vad klienten skickar. Klienten prövar samma gränser innan något skickas (`client/src/lib/uploads.ts`), och serverns fel visas på svenska via `serverMessages.ts`.

Länkarna till bilagor och inlämnade filer skapas i ett anrop per lista (`GetDownloadUrlsAsync`, Supabases `object/sign/{bucket}` med alla sökvägar), och filen laddas ner under sitt riktiga namn: `download=` på Supabase-länken, `name=` på disklänken (som ger `Content-Disposition`). Att ta bort en fil som redan är borta är inget fel, varken på disk eller i Supabase (massborttagningen `DELETE object/{bucket}`).

Bara elever som uppgiften har delats ut till (länkens grupp, eller en grupp som läser länkens kurs) kan lämna in, och bara när uppgiften är publicerad. Lärare, admin och andra elever får `403` och inget sparas. Eleven ser sina egna filer. Lärarna på kursen ser alla inlämningars filer. Admin ser dem inte, och en fil nås bara via en kortlivad länk som skapas när inlämningen läses.

## Profilbilder

Alla användare kan ladda upp en egen profilbild under _Inställningar → Profil_:

- `POST /api/me/avatar` (fältet `file`) sparar bilden och svarar med den nya profilen (`MeDto`)
- `DELETE /api/me/avatar` tar bort den

Bara png, jpg, gif och webp, högst 5 MB (`AvatarPolicy`). Bilden sparas under `avatars/{userId}/{guid}` och användaren har en bild åt gången: en ny bild får en ny nyckel och den gamla tas bort.

Svaren som visar personer (`MeDto`, `GroupMemberDto`, `CourseStaffDto`, flödets och kommentarernas författare och inlämningar) har en adress `/api/avatars/{userId}/{guid}`, eller `null` utan bild. Adressen fungerar utan inloggning, eftersom en `<img>` inte skickar med token. Den skickar vidare (302) till en kortlivad länk, och svaret cachas i fem minuter. Den slumpade guiden gör att adressen inte går att gissa, och den slutar fungera när bilden byts eller tas bort. Utan bild visas initialerna som förut.

## Inställningar

Lagringen väljs så här: Supabase när `Storage:Supabase:Url` och `ServiceRoleKey` finns, annars disk när `Storage:Disk:Path` finns. Utan någon av dem lagras filerna i minnet i Development (borta när appen stängs av). I andra miljöer går det då inte att spara eller läsa filer, men appen startar.

### På disk

Med `Storage:Disk:Path` (till exempel `/data/files`, en volym i Docker) sparas varje fil i mappen under sin nyckel, med innehållstypen i en fil bredvid (`.type`). Länkarna ser ut som `/files/{nyckel}?expires=...&sig=...` och lämnas ut av appen själv (`FileEndpoints`). Signaturen är en HMAC som härleds ur `Auth:Jwt:Key`, så ingen ny hemlighet behövs. En länk som har ändrats, gått ut eller pekar utanför mappen ger `404`. Säkerhetskopiera mappen tillsammans med databasen.

För att köra mot Supabase lokalt: skapa en privat bucket, t.ex. `files`, och lägg `Storage__Supabase__Url` och `Storage__Supabase__ServiceRoleKey` i Infisical (`dev`), se [utveckling.md](utveckling.md).

Service role-nyckeln går förbi Supabases row level security och är en hemlighet. Var den sätts i prod: [konfiguration.md](konfiguration.md#fillagring).
