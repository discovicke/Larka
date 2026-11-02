# Roller och flöden

Admin bestämmer vem som undervisar vad, och läraren bestämmer vad som händer i kursen. Eleven läser och lämnar in.

Modellen beslutades 30 oktober 2026 och byggs i #269. Per kurs-lärare är #274, klassidans flöde är #275 och separationen av admin är #276.

## Roller

Den globala rollen säger vad du administrerar. Din koppling till en kurs eller klass säger vad du gör där.

|                                        | Admin    | Lärare, egna kurser | Mentor, egen klass | Elev               |
| -------------------------------------- | -------- | ------------------- | ------------------ | ------------------ |
| Skola, användare, integrationer        | Hanterar | –                   | –                  | –                  |
| Klasser, inskrivning, lärare på kurs   | Hanterar | Läser klasslistan   | Läser klasslistan  | Egna klasskamrater |
| Skapa, ta bort och arkivera kurser     | Hanterar | –                   | –                  | –                  |
| Beskrivning, veckor, innehåll, nyheter | Läser    | Hanterar            | Läser              | Läser publicerat   |
| Inlämningar och betyg                  | –        | Hanterar            | Läser              | Bara egna          |
| Lämna in, kommentera, bocka av         | –        | –                   | –                  | Ja                 |

Admin har ingen genväg in i undervisningen. Är en lärare sjuk tilldelar admin en vikarie, eller sig själv om hen också undervisar. Betyget visar alltid vem som formellt var lärare.

Eleven ser namn på dem i samma grupp, och kontaktuppgifter till kursens lärare. Aldrig andras inlämningar, betyg eller framsteg.

## Skolans år

Sidorna byggs efter arbetet, inte efter databasens objekt.

1. **Sätt upp klassen (admin, en gång).** Skapa klassen, lägg in eleverna och utse mentor.
2. **Planera terminen (admin, några gånger om året).** Skapa varje kurs från klassens sida, med datum och lärare. Har kursen gått förut utgår admin från den tidigare kursen, så följer namn, omfattning, lärare och valda veckor med.
3. **Förbered kursen (lärare).** Kursen syns i Idag under Behöver förberedas när beskrivningen är tom, eller när kursen inte har börjat och saknar publicerade delar. Läraren skriver beskrivningen, bygger veckorna eller kopierar dem från ett tidigare tillfälle och publicerar.
   Allt kopierat är utkast. En vecka med bara utkast syns inte för eleverna, oavsett datum, tills läraren väljer Publicera veckan eller Publicera alla veckor. Delarna syns sedan på sina egna datum.
4. **Kör kursen (lärare och elev, varje vecka).** Läraren publicerar och bedömer, eleven läser och lämnar in. Här ligger nästan all tid i appen.
5. **Stäng och upprepa (lärare, sedan admin).** Läraren bedömer klart, kursen tar slut och nästa tillfälle utgår från det gamla.

## Var saker hör hemma

- **Klassen** är en årskull och håller elever och mentor. Den har ingen beskrivning.
- **Kursen** håller lärarna, veckorna och innehållet. Varje rad är ett tillfälle med egna datum, så de flesta kurser hör till en klass.
- **Klassens sida** är admins arbetsyta. Ny kurs är den primära handlingen där och finns bara på en klass eller grupp, inte på Alla grupper: först väljer admin en tidigare kurs eller en tom kurs, sedan namn, kod, omfattning, datum och lärare, och kursen skapas, kopplas till klassen och får sina lärare i ett flöde. Att ordna visar vad som återstår tills klassen har kurser, elever och mentor. Koppla kurs är för en kurs som delas mellan klasser. Varje kursrad visar lärarna, och en kurs utan lärare har märket Ingen lärare. Där lägger admin också in elever, en i taget eller genom att klistra in en lista med namn och e-post (nya elever skapas utan lösenord), och utser klassens mentor. Mentor är en roll i klassen och en klass har en mentor: att byta tar bort den förra ur klassen.
- **Kursbeskrivningen** är lärarens text och valfri. Admin skriver den inte när kursen skapas.
