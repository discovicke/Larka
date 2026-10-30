# Fillagring

Filer (bilagor och inlämningar) ligger i en privat bucket i Supabase Storage, bakom `IFileStorage`. Databasen sparar bara nyckeln (`content/{contentId}/{guid}`, `submissions/{submissionId}/{guid}`), och svaren innehåller en länk som gäller i 10 minuter och skapas när filen läses. Åtkomstkontrollerna görs i backend innan länken skapas.

## Uppladdning

Uppladdning sker med `multipart/form-data`:

- bilagor till innehåll: `POST /api/content/{id}/attachments` (fältet `file`)
- inlämningar: `POST /api/links/{id}/submissions` (fälten `bodyMd` och `files`)

Högst 20 MB per fil, 10 filer och 50 MB per anrop, och bara filtyper som finns i `FileUploadPolicy` (pdf, bilder, txt, md, Office-filer och zip). Typen sätts från filändelsen, inte från vad klienten skickar.

Eleven ser sina egna filer. Lärarna på kursen ser alla inlämningars filer, och mentorn ser filerna för eleverna i sin klass. Admin ser dem inte, och en fil nås bara via en kortlivad länk som skapas när inlämningen läses.

## Inställningar

Utan inställningar lagras filerna i minnet i Development (borta när appen stängs av). I andra miljöer går det då inte att spara eller läsa filer, men appen startar.

För att köra mot Supabase lokalt: skapa en privat bucket, t.ex. `files`, i Supabase-projektet och ställ in:

```bash
dotnet user-secrets set Storage:Supabase:Url "https://<projekt>.supabase.co" --project backend
dotnet user-secrets set Storage:Supabase:ServiceRoleKey "<service role key>" --project backend
```

Service role-nyckeln går förbi Supabases row level security och är en hemlighet. Var den sätts i prod: [konfiguration.md](konfiguration.md#fillagring).
