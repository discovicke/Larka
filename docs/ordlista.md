# Lärka – ordlista

Gränssnittet och koden använder inte alltid samma ord. Tabellen visar vad som heter vad, så att en issue eller en kodgranskning inte missförstår varandra. Datamodellen förklaras i [er-diagram.md](er-diagram.md).

| I gränssnittet | I koden                           | Betyder                                                                                                          |
| -------------- | --------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| material       | `Content`                         | Själva materialet: en nyhet, teori eller uppgift med titel och text. Lever fristående och tillhör ingen kurs.    |
| del            | `ContentLink`                     | Materialets plats i en kurs (eller grupp), med publiceringsdatum (`PublishAt`) och deadline (`DueAt`).           |
| vecka          | `Module`                          | En vecka i en kurs. Innehåller delar och kan ha ett eget datum då den öppnar.                                    |
| klass          | `Group` med `Type = Class`        | En årskull, till exempel `.NET25`. Andra grupptyper heter kommitté och grupp, och alla är `Group` i koden.       |
| uppgift        | `Content` med `Type = Assignment` | Material som eleverna lämnar in något på. Inställningarna ligger i `AssignmentSpec`.                             |
| inlämning      | `Submission`                      | En elevs svar på en uppgift, kopplat till delen (`ContentLink`) och inte till materialet.                        |
| kursomgång     | `CourseRunService`                | Ingen egen tabell: en ny `Course` för en annan klass, kopierad från en källkurs (`POST /api/courses/{id}/runs`). |
