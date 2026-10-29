# Design

Mockupen för hela frontend: [Mockup](https://claude.ai/artifact/PPjCF4RnekzyfrvJrYF8LC)

Den täcker inloggning, startsida, kurssida (innehåll, nyheter, diskussion, om/personal), uppgiftssida med valfri GitHub-inlämning, notiser, kalender, material, studieplan och betyg, LIA, profil och inställningar, mobilvyer, lärarvyer (kursredigering, skapa uppgift, inlämningar, bedömning, avbockning på plats) och admin-integrationer.

Bygger på vår shadcn-vue-setup i `client/`: färgpaletten Lärka (tavelgrön och blyertsgul), radie 0.45rem, typsnitten Inter och Roboto, Phosphor-ikoner.

Mockupen är en referens. Sidorna byggs med våra egna komponenter och ska följa dess mått, färger och avstånd.

Beslutad i #16. Sidorna byggs i #60 (lärare skapar innehåll) och #65 (GitHub-uppgifter).

## Färg med avsikt

Färg betyder något. Varje steg har sin egen token, och vi blandar dem inte:

- **Neutralt:** `muted` / `muted-foreground` (läst, kommande).
- **Ny / info:** `info` och `link` (oläst-prick, antal olästa, nyheter, länkar).
- **Klart:** `success` (inlämnat, godkänt, avslutat).
- **Ditt drag:** `primary` (primär handling, antal "Att bedöma", framsteg).
- **Snart:** `warning` (deadline inom 48 h, komplettering begärd, saknad inställning). Orange i mörkt läge så att den inte liknar primärgult.
- **Allvarligt:** `destructive` (försenat, IG/misslyckat, fel, radera).

Logons röda prick (#D2483A) är bara varumärke och används aldrig för status.

Aktiv menypunkt i sidomenyn är "krita": kritvit fyllning med tavelgrön text i ljust läge, 14 % kritvit med vit text i mörkt. Hover och tryck är en svag ton av förgrundsfärgen, så att bara den aktuella sidan får krita.
