# Lärka

Hela skoldagen på ett ställe, från första lektionen till sista inlämningen.

Varumärket och grunderna för Lärkas gränssnitt. Tokens står i `client/src/assets/index.css`, färgernas betydelse och statusreglerna i [design.md](design.md). Det här dokumentet upprepar dem inte, så att det bara finns ett facit.

[Lärka Design System](https://claude.ai/artifact/XZcmpUiSbG8Eo9t8yfCPym) är samma innehåll som en bläddringsbar sida med förhandsvisningar. Skiljer de sig åt gäller repot.

## Vad Lärka är

Lärka är en lärplattform för yrkeshögskolor, byggd som en bättre LearnPoint. Eleven läser och lämnar in, läraren skapar och bedömer, och admin ordnar skolan. Namnet är en fågel som sjunger först på morgonen, med ordet _lär_ inbyggt.

## Principer

1. **Tyst före krom.** Lägg inte till kort, ramar eller färg för att det ska se ordnat ut. Det mesta på en sida är text på papper.
2. **Färg betyder något.** En färg har en betydelse och används bara för den. Se [design.md](design.md#status).
3. **Ett läge, ett ställe.** Samma sak sägs inte två gånger på en sida.
4. **Samma former, våra färger.** Allt byggs med shadcn-vue-komponenterna i `components/ui`. Vi byter färger, inte former. En ny komponent kräver ett skäl.
5. **Rätt saker först.** Det eleven behöver nu står överst, också på mobil. Mobil är inte en krympt desktop: listor staplas och listor och innehåll delas upp på egna sidor.

## Röst och ton

Som en bra mentor: kort, vänlig, konkret. All text i appen är på svenska.

- **Säg vad som händer.** Knappen säger exakt vad den gör: "Lämna in", inte "Skicka".
- **Datum före känsla.** "Stänger fredag 23.59" i stället för "Snart försent!".
- **Du, inte användaren.** Tilltala eleven direkt. Inga systemord som "entitet" eller "resurs".
- **Fel säger vad som gick fel och vad man gör.** Ingen ursäkt och ingen felkod.

| Skriv så                            | Inte så                                   |
| ----------------------------------- | ----------------------------------------- |
| Inlämnat. Läraren får en notis.     | Din inlämning har skickats framgångsrikt! |
| Filen är för stor. Max 25 MB.       | Ett fel uppstod (413).                    |
| Inga uppgifter kvar den här veckan. | Inga objekt att visa.                     |

Fasta ord: _del_ (lektion, uppgift eller nyhet i en kurs), _vecka_, _kurs_, _klass_, _grupp_, _elev_, _lärare_, _mentor_. Lägenas exakta ord står i [design.md](design.md#status).

## Färg

Varje färg kommer från ett svenskt klassrum: tavelgrön, blyertsgul, bläckblå, marginalröd, häftespapper, kritvit. Ljust läge är papper, mörkt läge är neutral grafit. Sidomenyn är tavelgrön i båda lägena, mörkare i mörkt.

Använd alltid tokens. Hårdkodade Tailwind-färger som `emerald-500` eller `amber-50` hör inte hemma i vyerna.

Vad varje färg betyder står under [Färg med avsikt](design.md#färg-med-avsikt) i design.md.

Logons gula linje är bara varumärke. Den betyder aldrig något i gränssnittet. Marginalröd finns inte i logon, eftersom röd betyder fel.

## Typografi

Appen använder presetets typsnitt: **Inter** för text och **Roboto** för rubriker, med vikt 600 och `tracking -0.01em` på rubriker. Brödtext i uppgifter och nyheter är högst 68 tecken bred.

**Familjen Grotesk** används bara utanför appen, som på presentationer. Ordmärket är egenritat så att dess ä är samma som i märket, så sätt det aldrig i typsnittet. Atkinson Hyperlegible och IBM Plex Mono från det första brand kitet används inte.

## Form och avstånd

Radien är `0.45rem` på knappar och fält, `11px` på kort och full rundning på märken. Kort har en tunn kant och ingen skugga. Avstånd följer Tailwinds skala med `gap`. Sidan har 16 px marginal på mobil.

Inga vänsterkanter eller färgade insidor på rundade rader och kort. Det är en AI-klyscha, och vald rad markeras med `bg-accent`.

## Logo

Logon är ett kritvitt geometriskt ä med en blyertsgul linje under, på en tavelgrön platta. Det är blyertsunderstrykningen från gränssnittet: pennans spår i stället för en bild av en penna. Ordmärket skrivs med gemener, lärka, med samma linje under _lär_. Luft runt märket är minst en fjärdedel av plattans bredd.

| Fil                                                               | Används för                                                   |
| ----------------------------------------------------------------- | ------------------------------------------------------------- |
| [`larka-mark.svg`](assets/larka-mark.svg)                         | Utanför appen, 32 px och större.                              |
| [`larka-mark-liten.svg`](assets/larka-mark-liten.svg)             | 24 och 16 px, med större bokstav och tjockare linje. Favicon. |
| [`larka-mark-pa-tavla.svg`](assets/larka-mark-pa-tavla.svg)       | Tavelgrön bakgrund, utan platta. Så står det i appen.         |
| [`larka-mark-enfarg.svg`](assets/larka-mark-enfarg.svg)           | Tryck och stämplar.                                           |
| [`larka-ordmark.svg`](assets/larka-ordmark.svg)                   | Ordmärket på papper.                                          |
| [`larka-ordmark-pa-tavla.svg`](assets/larka-ordmark-pa-tavla.svg) | Ordmärket på tavelgrön bakgrund.                              |

Rita aldrig om märket: det är optiskt centrerat, och ett ä som ser rätt ut i ett försök är sällan centrerat. I appen är det `AppLogo.vue`, utan platta, eftersom sidomenyn och inloggningens panel redan är tavlan.

Den gula linjen är svag på vitt. Ordmärket får aldrig bero på den för att gå att läsa.

## Ikoner

Phosphor via `@phosphor-icons/vue`, i vikten regular, `size-4` som standard. Ikoner står tillsammans med text. Rena ikonknappar har `aria-label`. Emoji används inte i gränssnittet.

## Signaturdetaljer

| Detalj                            | Läge                   | Regel                                                                                                     |
| --------------------------------- | ---------------------- | --------------------------------------------------------------------------------------------------------- |
| Betygsstämpeln                    | I appen                | IG, G och VG som en lätt sned stämpel. Badge-varianterna `gradeFail`, `gradePass` och `gradeDistinction`. |
| Krita                             | I appen                | Aktiv menypunkt är kritvit. Kritvit fyllning i ljust läge, 14 % kritvit i mörkt.                          |
| Blyertsunderstrykning             | I appen                | Gul understrykning på viktiga ord i en uppgift, en per stycke. Samma linje som i logon.                   |
| Marginallinje och linjerat papper | Bara på presentationer | Skulle kräva nya former i appen.                                                                          |

## Komponenter

Allt byggs med Vue-komponenterna i repot.

- `client/src/components/ui`: shadcn-vue, stil `reka-nova`. Ändra helst bara varianter.
- `client/src/components/layout/AppLogo.vue`: logon.

## Se också

- [Design](design.md): mockupen, färgernas betydelse och status
- [UI-mönster](ui-monster.md): återkommande lösningar i gränssnittet
- [Roller och flöden](roller-och-floden.md): vem som gör vad och skolans år
- [Designhistorik](design-historik.md): källor och beslut som ersatte något
