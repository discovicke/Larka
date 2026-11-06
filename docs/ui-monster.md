# Mönster

Återkommande lösningar i appen. Använd dem innan du hittar på något nytt.

## Lista och innehåll

På desktop står listan och innehållet sida vid sida. Under `md` är de två sidor: utan `?post` syns bara listan, med `?post` bara innehållet, med en bakåtlänk överst. Telefonens bakåtknapp går tillbaka till listan och innehållet börjar högst upp. Koden är `useMobileDetail`, som används av Nyheter och Inkorg.

## Del i en kurs

Typ och vecka står på en liten rad ovanför titeln. Veckan leder tillbaka till veckans lista, så sidan har ingen egen tillbakaknapp. Under titeln står bara deadlinen som gäller nu, med datumet i fetstil och tiden kvar som vanlig text ("om 9 dagar"). Deadline snart och Försenad är märken i stället för tiden kvar. Har eleven en egen deadline eller en kompletteringsdeadline står den under titeln, och den ordinarie står i panelen.

Övriga fakta står i panelen bredvid innehållet, med etikett till vänster och värde till höger (`PartFacts`): Lämnas in, Bedöms (IG / G / VG, hela namnen som tooltip) och datum som inte står under titeln. Eleven behöver dem när hen lämnar in, så de står nära knappen. Din status är panelen till höger på desktop och står före innehållet på mobil, så att eleven ser läget och knappen direkt. Knappen för att ångra säger bara Ångra, eftersom läget redan står ovanför.

## Delens sida för lärare

Läraren och admin öppnar en del läsbar på `courses/:courseId/parts/:linkId`. Klicket på en del öppnar alltså inte redigeraren. Redigera ligger uppe till höger, och efter sparandet kommer läraren tillbaka hit. Rubriken har samma typ, vecka och deadline som elevens. Panelen Läge visar publicering, inlämnade, hur många som väntar på bedömning, och under dem inlämningssätt, betygsskala och kompletteringar. Uppgifter med inlämning har flikarna Innehåll och Inlämningar, där den andra öppnas med `?tab=inlamningar`. Teori har inga flikar. Eleven har sin egen vy av delen på kurssidan.

## Tabeller på mobil

En tabell får aldrig tvinga sidan i sidled. Under `md` blir varje rad ett staplat block, med titeln överst och handlingen till höger. Kolumner som inte behövs på mobil döljs. Se Alla grupper och Inlämningar.

## Rubrikrad med handlingar

Knappar i en rubrikrad linjerar med kolumnens högerkant. Läsbredden `max-w-[68ch]` sätts på texten, inte på avsnittet runt den.

## Tomma lägen

Använd `Empty`. Säg vad som kommer att synas här och vem som lägger dit det: "Nyheter som läraren publicerar i kursen visas här."

## Bevis för UI-ändringar

Varje UI-ändring visas med före- och efterbilder på 1280 och 390 px innan pull requesten öppnas, i ljust läge och i mörkt när färger ändras.
