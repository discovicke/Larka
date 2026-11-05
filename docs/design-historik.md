# Källor och historik

Allt här kommer från de här dokumenten och från koden. Äldre dokument finns kvar som arkiv, men det här systemet gäller före dem.

## Källor

| Dokument                                                                        | Vad det är                             | Läge                   |
| ------------------------------------------------------------------------------- | -------------------------------------- | ---------------------- |
| [Lärka Brand Kit](https://claude.ai/artifact/UosqL11FYWJb6Bvs2KxrN9)            | Namn, logo, palett, ton                | Grunden, delvis ersatt |
| [Lärka × shadcn](https://claude.ai/artifact/HYZRKPx6RfpyWvBPKN8FoX)             | Så blev kitet shadcn-vue-tokens        | Inbyggt i appen        |
| [Lärka accent mocks](https://claude.ai/artifact/H4sg5DG9bKmxb3wC53ARpN)         | Färgstegen och kritan                  | Inbyggt i appen        |
| [Lärka Mockar](https://claude.ai/artifact/11jgiVrYnmmaNkwPZEiPbe)               | Appen före och efter nya färger        | Arkiv                  |
| [Lärka Palettval](https://claude.ai/artifact/9J1eknuEoG6PuFsW3H682N)            | Valda bort: Gryning, Faluröd, Kulspets | Arkiv                  |
| [Lärka Gryningsvarianter](https://claude.ai/artifact/CEQtv45SY9tfesT6bLALdC)    | Varianter av Gryning, valda bort       | Arkiv                  |
| [Learnpoint – nästa version](https://claude.ai/artifact/PPjCF4RnekzyfrvJrYF8LC) | Mockupen för sidornas layout och mått  | Gäller för layout      |
| [Ny kursomgång flow](https://claude.ai/artifact/DxQ1BGjwkCSYmcKDYA9zBL)         | Flödet för en ny kursomgång            | Inbyggt i appen        |
| [Lärka Review Sheet](https://claude.ai/artifact/78d7dtrDjt2WrvhTJLFK5r)         | Bildbevis för UI-ändringar             | Löpande                |
| [Lärka Logo Concepts](https://claude.ai/artifact/3P8ktLp1nVeVuWiE4HeKGi)        | Logon, med blindtest av alternativen   | Inbyggt i appen        |
| `docs/design.md`, `client/src/assets/index.css`                                 | Reglerna och tokens i repot            | Facit                  |

## Beslut som ersatte något

| Datum  | Tidigare                                    | Nu                                                                                          | Varför                                                                                     |
| ------ | ------------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| 29 okt | Röda pricken betyder oläst                  | Röda pricken är bara varumärke. Oläst är blått.                                             | Färgstegen ska ha en betydelse per färg.                                                   |
| 29 okt | Grön tavla som mörkt läge                   | Neutral grafit                                                                              | Ytorna gick inte att skilja åt i den gröna.                                                |
| 29 okt | Familjen Grotesk och Atkinson i appen       | Inter och Roboto                                                                            | Presetet behölls, bara färgerna byttes.                                                    |
| 29 okt | Stämplar lämnas utanför appen               | IG, G och VG som stämpel                                                                    | Infört i #261, som egen Badge-variant.                                                     |
| 29 okt | Aktiv menypunkt med gul ton och vänsterkant | Krita                                                                                       | Vänsterkanter är en AI-klyscha.                                                            |
| 30 okt | Avslutad är grön, Pågår är fylld            | Båda är neutrala                                                                            | Normala lägen ska vara tysta.                                                              |
| 30 okt | Komplettering är gul                        | Komplettera är röd                                                                          | Eleven måste göra något nu.                                                                |
| 30 okt | Klar när uppgiften är inlämnad              | Inlämnad tills den är bedömd                                                                | Klar ska betyda klar.                                                                      |
| 30 okt | Lärare hör till klassen                     | Lärare hör till kursen                                                                      | Annars går det inte att skilja på en lärares kurser.                                       |
| 30 okt | Ä på gul platta med röd prick               | Kritvitt ä med gul linje på tavelgrön platta                                                | Röda pricken såg ut som ett underkänt prov.                                                |
| 30 okt | Ordmärket sätts i Familjen Grotesk          | Egenritat ordmärke                                                                          | Dess ä ska vara samma som i märket.                                                        |
| 5 nov  | Tavelgrön och blyertsgul som huvudpalett    | Tavelgrön primary, gul VG och grå IG i ljust; sidebar och dark mode behåller tavelgrön/volt | Varmare och mindre blått, ingen röd/gul/grön flagga, och volten används bara där den syns. |

## Avvikelser att känna till

- **Sidomenyn i mörkt läge:** kitet och beslutet 29 oktober säger grafit `#0d0f0e`, men appen har mörk tavelgrön `#123823`. Tokens här följer appen.
- **`--warning` i mörkt läge:** kitet har volt `#dfff00`, appen har amber `#fbbf24` så att varningar inte förväxlas med `primary`.
- **`--muted` i mörkt läge och `--sidebar-accent`:** värdena skiljer sig från kitet, och appens värden gäller.
