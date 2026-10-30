# Design

Mockupen för hela frontend: [Mockup](https://claude.ai/artifact/PPjCF4RnekzyfrvJrYF8LC)

Den täcker inloggning, startsida, kurssida (innehåll, nyheter, diskussion, om/personal), uppgiftssida med valfri GitHub-inlämning, notiser, kalender, material, studieplan och betyg, LIA, profil och inställningar, mobilvyer, lärarvyer (kursredigering, skapa uppgift, inlämningar, bedömning, avbockning på plats) och admin-integrationer.

Bygger på vår shadcn-vue-setup i `client/`: färgpaletten Lärka (tavelgrön och blyertsgul), radie 0.45rem, typsnitten Inter och Roboto, Phosphor-ikoner.

Varumärket, logon och tonen står i [varumarke.md](varumarke.md).

Mockupen är en referens. Sidorna byggs med våra egna komponenter och ska följa dess mått, färger och avstånd.

Beslutad i #16. Sidorna byggs i #60 (lärare skapar innehåll) och #65 (GitHub-uppgifter).

## Färg med avsikt

Färg betyder något. Varje steg har sin egen token, och vi blandar dem inte:

- **Neutralt:** `muted` / `muted-foreground` (läst, kommande).
- **Ny / info:** `info` och `link` (oläst-prick, antal olästa, nyheter, länkar, inlämnat och schemalagt som väntar på någon annan).
- **Klart:** `success` (eleven har gjort klart något, godkänt). För statusmärken gäller tabellen under Status.
- **Ditt drag:** `primary` (primär handling, antal "Att bedöma", framsteg).
- **Snart:** `warning` (deadline inom 48 h, saknad inställning). Orange i mörkt läge så att den inte liknar primärgult.
- **Allvarligt:** `destructive` (försenat, komplettera, IG/misslyckat, fel, radera).

Logons röda prick (#D2483A) är bara varumärke och används aldrig för status.

Aktiv menypunkt i sidomenyn är "krita": kritvit fyllning med tavelgrön text i ljust läge, 14 % kritvit med vit text i mörkt. Hover och tryck är en svag ton av förgrundsfärgen, så att bara den aktuella sidan får krita.

## Status

Statusmärken (pills) visar läget hos något, och bara det. Alla lägen finns på ett ställe, `client/src/lib/status.ts`, och ritas med `StatusBadge` (`client/src/components/StatusBadge.vue`). Ett blad med alla lägen i verklig storlek finns på `/dev/status`. Nya lägen läggs till i `status.ts`, aldrig som egna klasser i en vy.

### Regler

1. **Ett läge visas på ett ställe per sida.** Står läget redan i en statusruta, rubrik eller rad ska det inte upprepas som märke i sidhuvudet, i listan eller i en panel bredvid.
2. **Normala lägen är tysta.** Ett läge som inte kräver något (Pågår, Utkast, Publicerad) är en kontur utan färg. Färg används bara för det som behöver uppmärksamhet.
3. **Märken är till för lägen.** Fakta som betygsskala och inlämningssätt är vanlig text (till exempel i metaraden under rubriken), inte märken. Ett datum som inte är nära är också text.
4. **Färgen har en betydelse var.** Ingen ton får användas för något annat än sin betydelse.
5. **Betygsstämplarna IG, G och VG** behåller sina egna varianter (`gradeFail`, `gradePass`, `gradeDistinction`) och ingår inte i statussystemet.

### Toner

| Ton           | Badge-variant | Betyder                     | Lägen                                                                                                   |
| ------------- | ------------- | --------------------------- | ------------------------------------------------------------------------------------------------------- |
| Neutral       | `outline`     | Information, inget att göra | Kommande, Pågår, Avslutad, Utkast, Delvis publicerad, Publicerad, Låst, Att göra, Inte inlämnad, Bedömd |
| Info (blå)    | `info`        | Väntar på någon annan       | Schemalagd, Inlämnad                                                                                    |
| Varning (gul) | `warning`     | Behöver uppmärksamhet snart | Deadline snart                                                                                          |
| Fel (röd)     | `destructive` | Behöver åtgärd nu           | Försenad, Komplettera                                                                                   |
| Klart (grön)  | `success`     | Eleven är klar              | Klar                                                                                                    |

Inga nya färger: tonerna använder de befintliga tokens `info`, `warning`, `destructive` och `success`. Avslutad är alltså neutral och inte grön, eftersom grönt bara betyder att eleven har gjort klart något.

### Familjer

- **Kursens läge** (`lib/courseStatus.ts`): Kommande, Pågår, Avslutad. Alla neutrala.
- **Publicering** (lärarvyer): Utkast, Schemalagd (med datum som text i märket), Delvis publicerad, Publicerad.
- **Elevens framsteg**: Att göra, Låst, Klar.
- **Inlämning och bedömning**: Inte inlämnad, Inlämnad, Komplettera, Bedömd. "Sen inlämning" är vanlig text vid inlämningsdatumet, inget läge. Bedömd visar betyget som stämpel bredvid.
- **Deadline**: Deadline snart (inom två dygn) och Försenad. En deadline utan läge är ett datum i klartext. Klara och inlämnade delar är aldrig försenade.

GitHub-uppgiftens teststatus ingår inte än.

### Rätt läge för en inlämnad uppgift

En inlämnad men obedömd uppgift är **Inlämnad** (blå), inte Klar. Klar är bara ett läge eleven själv sätter (teori) eller en uppgift som är godkänd. Att inlämnat räcker för att låsa upp nästa del är en regel för upplåsning och ändrar inte vad eleven ser som status.

Elevens läge för en del räknas fram på ett ställe, `partStatusKey` i `status.ts`, och används för rubriken i Din status, ikonen, färgen och märket i listan. Ordningen är: låst, sedan inlämningen (Inlämnad, Komplettera, Klar för godkänd eller utan betyg, Bedömd för IG), sedan klarmarkering (Klar), annars Att göra. `isCompleted` från API:t räknar framsteg och låser upp nästa del och avgör inte vad som visas.

Antal, till exempel "4 att bedöma" och räknare i flikar, är inga lägen och ligger utanför `status.ts`.
