using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

internal static partial class DemoCatalog
{
    private static CourseSpec Databases(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_DB",
        "Databaser",
        """
        Relationsdatabaser, SQL och dataåtkomst med Entity Framework Core mot PostgreSQL.

        **Kursmål**

        1. Att modellera data i en relationsdatabas och normalisera till tredje normalformen.
        2. Att skriva SQL med SELECT, JOIN, aggregering och delfrågor.
        3. Att använda transaktioner för att skydda datas konsistens.
        4. Att använda index och läsa en query plan.
        5. Att nå en databas med EF Core, code first och migrationer.
        """,
        25,
        start,
        end,
        author,
        [
            Week(
                "Vecka 1 – Normalisering, SQL och transaktioner",
                "Normalisering, frågor, joins och transaktioner.",
                start,
                [
                    Lesson(
                        "Teori: Normalisering",
                        """
                        En relationsdatabas lagrar data i tabeller med rader och kolumner. Poängen med att dela upp
                        data i flera tabeller är att varje faktum bara ska finnas på ett ställe. Står en kunds adress
                        på varje order måste du ändra den på hundra ställen när kunden flyttar, och förr eller senare
                        glömmer du ett. Det kallas en **uppdateringsanomali**, och normalisering är sättet att
                        bli av med den.

                        Varje tabell har en **primärnyckel** som unikt identifierar raden, och tabeller kopplas ihop med
                        **främmande nycklar** som pekar på en annan tabells primärnyckel. Databasen kan se till att
                        du inte skapar en order för en kund som inte finns, men bara om du deklarerar
                        främmande nyckeln.

                        **Första normalformen (1NF)**: varje kolumn innehåller ett enda värde, och det finns inga
                        upprepade grupper. En kolumn `telefon` med "070-1, 070-2" bryter mot det, liksom kolumnerna
                        `produkt1`, `produkt2` och `produkt3`. **Andra normalformen (2NF)**: varje kolumn som inte
                        är nyckel beror på hela primärnyckeln, inte bara en del av en sammansatt nyckel. **Tredje
                        normalformen (3NF)**: kolumner som inte är nyckel beror bara på nyckeln och inte på varandra.
                        Står både `postnummer` och `ort` i kundtabellen beror orten på postnumret och ska
                        flyttas till en egen tabell. En minnesregel är att varje kolumn ska bero på "nyckeln, hela
                        nyckeln och inget annat än nyckeln".

                        ```sql
                        CREATE TABLE kund (
                            id      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                            namn    text NOT NULL,
                            epost   text NOT NULL UNIQUE
                        );

                        CREATE TABLE ordrar (
                            id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                            kund_id   integer NOT NULL REFERENCES kund (id),
                            skapad    timestamptz NOT NULL DEFAULT now()
                        );

                        CREATE TABLE orderrad (
                            order_id    integer NOT NULL REFERENCES ordrar (id),
                            produkt_id  integer NOT NULL REFERENCES produkt (id),
                            antal       integer NOT NULL CHECK (antal > 0),
                            PRIMARY KEY (order_id, produkt_id)
                        );
                        ```

                        Normalisering är ett verktyg och inget självändamål. Ibland väljer man medvetet att
                        **denormalisera**, till exempel spara orderns totalsumma i ordern för att slippa räkna om den,
                        men gör det som ett beslut och inte av slarv. Skriv ner varför.

                        ## Normalisering i praktiken

                        Prova att ta ett kalkylblad med fakturor, där kunden, adressen, produkterna och priserna
                        står på samma rad, och dela upp det i tabeller. Ställ frågan för varje kolumn: vad
                        identifierar den här raden, och beror den här kolumnen på det? Börja med att
                        hitta objekten i verkligheten (kund, order, produkt), för det är dem du ska ha tabeller för.
                        """,
                        "Normalisering"),
                    Lesson(
                        "Teori: SELECT, JOIN och aggregering",
                        """
                        Med SQL beskriver du vilken data du vill ha, och databasen räknar ut hur den hämtas. Det
                        grundläggande är `SELECT` (vilka kolumner), `FROM` (vilken tabell), `WHERE` (vilka rader)
                        och `ORDER BY` (i vilken ordning). Villkor kombineras med `AND`, `OR` och `NOT`, och
                        textmönster hittas med `LIKE` eller `ILIKE` (som struntar i versaler).

                        ```sql
                        SELECT namn, epost
                        FROM kund
                        WHERE epost ILIKE '%@example.com'
                        ORDER BY namn
                        LIMIT 10;
                        ```

                        Med `JOIN` slår du ihop rader från flera tabeller via främmande nycklar. En `INNER JOIN`
                        ger bara rader som har en matchning på båda sidor. En `LEFT JOIN` behåller alla rader från
                        vänstra tabellen och fyller med `NULL` där höger saknas, vilket är precis vad du vill
                        ha när du frågar "vilka kunder har aldrig beställt något?".

                        ```sql
                        SELECT k.namn, count(o.id) AS antal_ordrar
                        FROM kund k
                        LEFT JOIN ordrar o ON o.kund_id = k.id
                        GROUP BY k.id, k.namn
                        HAVING count(o.id) = 0;
                        ```

                        `GROUP BY` slår ihop rader till grupper så att du kan använda aggregatfunktioner som
                        `count`, `sum`, `avg`, `min` och `max`. `WHERE` filtrerar rader *före* grupperingen och `HAVING`
                        filtrerar grupperna *efter* den. Det är en klassisk tentafråga. En **delfråga** är en
                        `SELECT` inuti en annan och passar när du behöver ett värde att jämföra mot, till exempel
                        kunder som lagt fler ordrar än snittet.

                        Och så NULL. `NULL` betyder "okänt", inte noll och inte tom text. Ingenting är lika med
                        `NULL`, inte ens `NULL` självt, så `WHERE telefon = NULL` ger aldrig någon rad. Skriv
                        `IS NULL` och `IS NOT NULL`. Aggregat som `count(kolumn)` hoppar över `NULL`, medan
                        `count(*)` räknar raderna. Använd `COALESCE(telefon, 'saknas')` för att byta ut ett `NULL`
                        mot ett värde.

                        ## SQL i praktiken

                        Skriv frågor stegvis. Börja med den enklaste `SELECT *` mot den första tabellen, lägg till
                        en `JOIN` i taget och kontrollera antalet rader efter varje steg. Får du fler rader än du
                        väntat dig har du troligen glömt ett villkor i `ON`, och får du för få har du en
                        `INNER JOIN` där du ville ha en `LEFT JOIN`.
                        """,
                        "SELECT, JOIN och aggregering"),
                    Lesson(
                        "Teori: Transaktioner och ACID",
                        """
                        En **transaktion** är en grupp av ändringar som antingen lyckas allihop eller inte alls.
                        Tänk på en banköverföring: pengar ska dras från ett konto och läggas till på ett annat. Går
                        strömmen efter första steget har pengar försvunnit, och det vill ingen. Därför
                        omsluter man båda stegen i en transaktion.

                        ```sql
                        BEGIN;
                        UPDATE konto SET saldo = saldo - 500 WHERE id = 1;
                        UPDATE konto SET saldo = saldo + 500 WHERE id = 2;
                        COMMIT;
                        ```

                        Går något fel säger du `ROLLBACK` i stället för `COMMIT`, och ingenting har hänt. Transaktioner
                        brukar beskrivas med **ACID**: *Atomicity* (allt eller inget), *Consistency* (reglerna, som
                        främmande nycklar och `CHECK`, gäller före och efter), *Isolation* (samtidiga transaktioner
                        ser inte varandras halvfärdiga arbete) och *Durability* (efter commit finns ändringen
                        kvar, även om servern kraschar).

                        Isolation har nivåer. I PostgreSQL är standard `READ COMMITTED`, där du ser ändringar som
                        andra hunnit committa mitt i din transaktion. Med `SERIALIZABLE` beter sig databasen som om
                        transaktionerna körts en i taget, men du får vara beredd på att försöka igen om den avbryter en
                        av dem. Vill du låsa en rad medan du läser den, till exempel för att dra saldot, använder du
                        `SELECT ... FOR UPDATE`.

                        I EF Core är `SaveChanges()` redan en transaktion, så allt som sparas i samma anrop lyckas
                        eller misslyckas tillsammans. Behöver du flera anrop i samma transaktion skapar du den själv:

                        ```csharp
                        await using var transaktion = await db.Database.BeginTransactionAsync();
                        try
                        {
                            konto1.Saldo -= 500;
                            konto2.Saldo += 500;
                            await db.SaveChangesAsync();
                            await transaktion.CommitAsync();
                        }
                        catch
                        {
                            await transaktion.RollbackAsync();
                            throw;
                        }
                        ```

                        ## Transaktioner i praktiken

                        Håll transaktionerna korta. En transaktion som står öppen medan användaren funderar på sitt
                        lösenord håller kvar sina lås och blockerar andra. Gör förberedelserna först, öppna
                        transaktionen, gör ändringen och stäng den.
                        """),
                    Exercise(
                        "Övningsuppgift: SQL mot webbshopen",
                        """
                        För att öva på SQL ska vi utgå från följande schema för en liten webbshop. Du kan skapa det i
                        din egen PostgreSQL-databas med skriptet i kursmaterialet.

                        ```sql
                        kund      (id, namn, epost, ort)
                        produkt   (id, namn, pris, kategori)
                        ordrar    (id, kund_id -> kund.id, skapad)
                        orderrad  (order_id -> ordrar.id, produkt_id -> produkt.id, antal)
                        ```

                        Skriv en fråga för varje punkt och kontrollera att resultatet verkar rimligt:

                        - alla produkter i kategorin `Böcker`, billigaste först
                        - namn och epost för kunder i Umeå eller Luleå
                        - hur många ordrar varje kund har lagt (kunder utan ordrar ska med och visa 0)
                        - totalsumman för varje order (`antal * pris`, summerat per order)
                        - kunden som handlat för mest pengar totalt
                        - produkter som aldrig sålts
                        - alla kunder vars snittorder är högre än snittet för alla ordrar (använd en delfråga)

                        Prova gärna att skriva samma fråga med både `LEFT JOIN` och `NOT EXISTS` och jämför
                        resultatet. Ger de olika svar? Då har du hittat något att fundera på med NULL.

                        När du är klar kan du lägga till ett `CHECK (pris >= 0)` på produkt och försöka sätta ett
                        negativt pris, för att se vad databasen säger. Ingen inlämning.
                        """),
                    Assignment(
                        "Laboration: SQL-övningar",
                        """
                        I den här laborationen löser du tolv uppgifter mot övningsdatabasen. Du får ett
                        skript som skapar schemat och fyller det med data (se kursmaterialet), och du ska skriva
                        SQL-frågorna själv. Det ska vara vanlig SQL för PostgreSQL, inga tillägg i form av
                        ORM eller verktyg som genererar frågorna åt dig. Du får använda valfritt verktyg för att köra
                        dem (`psql`, pgAdmin, DBeaver).

                        Skriv frågorna för följande, en fråga per punkt:

                        - Urval och sortering
                          - alla kunder i en viss ort, sorterade på namn
                          - de fem dyraste produkterna
                          - produkter vars namn innehåller ett visst ord, oavsett versaler
                        - Joins
                          - orderrader med kundens namn och produktens namn
                          - kunder som aldrig lagt en order (med ==LEFT JOIN== och `IS NULL`)
                          - produkter som aldrig sålts
                        - Aggregering
                          - antal ordrar och total summa per kund
                          - total försäljning per produktkategori
                          - kunder som handlat för mer än 5 000 kr (med `HAVING`)
                        - Delfrågor och NULL
                          - kunder vars snittorder är högre än snittet för alla
                          - kunder utan telefonnummer, visade som "saknas" med `COALESCE`
                        - Normalisering
                          - uppgift tolv: tabellen `gammal_faktura` är inte normaliserad. Skriv `CREATE TABLE` för
                            en normaliserad modell och förklara vilken normalform varje ändring leder till

                        Redovisa varje uppgift med din fråga och en rad om hur du tänkte eller vad resultatet
                        blev. Hittar du på något fel i övningsdatabasen får du gärna säga till.

                        💡 Tips

                        Kontrollera antalet rader när du bygger frågorna. Ser det konstigt ut, ta bort `GROUP BY`
                        och titta på råa raderna. Jämför gärna med en kurskamrat: olika frågor kan ge samma svar,
                        och det är bra att se hur.

                        Det finns ofta mer än ett sätt att lösa en uppgift. Välj det du själv förstår, inte det som
                        ser mest avancerat ut.

                        ## Att lämna in

                        - [ ] Dina tolv frågor, i ordning, med en kort kommentar till varje
                        - [ ] En skiss eller `CREATE TABLE` för den normaliserade modellen i uppgift 12
                        - [ ] Antalet rader som varje fråga returnerar

                        **Betygskriterier**

                        Du blir godkänd om frågorna ger rätt resultat och du kan förklara dem. Laborationen examinerar
                        kursmål 1, "Att modellera data i en relationsdatabas och normalisera till tredje
                        normalformen", och kursmål 2, "Att skriva SQL med SELECT, JOIN, aggregering och delfrågor".
                        """,
                        Friday(start, 1),
                        GradeScale.PassFail,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Alla tolv är lösta. Uppgift 5 (kunder utan order) gjorde jag med `LEFT JOIN ... WHERE "
                            + "o.id IS NULL` och fick 4 rader. Uppgift 9 tog jag `HAVING sum(...) > 5000` efter "
                            + "att jag förstått att `WHERE` inte går att använda på aggregat.",
                            "Frågorna ligger i filen `svar.sql`. Den svåraste var uppgift 10 med delfrågan på snittordern, "
                            + "men jag skrev först snittet som en egen fråga och byggde ihop dem. I uppgift 12 delade "
                            + "jag upp fakturan i `kund`, `faktura` och `fakturarad` för att få 3NF.",
                            "Jag använde `COALESCE(telefon, 'saknas')` i uppgift 11. Jag försökte först skriva "
                            + "`WHERE telefon = NULL` och fick noll rader, vilket jag inte förstod förrän jag läste "
                            + "teorin igen.",
                            "Klart. Jag jämförde `LEFT JOIN`- och `NOT EXISTS`-versionen av uppgift 6 och de gav samma "
                            + "svar, 7 produkter.",
                            "Här är frågorna. Uppgift 12: ortnamnet beror på postnumret, så jag bröt ut `postort` till "
                            + "en egen tabell. Det är 3NF eftersom ingen kolumn beror på en annan kolumn som inte "
                            + "är nyckel."
                        ],
                        [
                            "Alla tolv är rätt och normaliseringen i uppgift 12 är tydligt motiverad. Godkänd.",
                            "Godkänd. Bra att du kontrollerade radantalen. I uppgift 8 hade du kunnat undvika en "
                            + "delfråga med en `JOIN`, men svaret är rätt.",
                            "Bra jobbat med `LEFT JOIN`-uppgifterna, de är de som flest snubblar på. Godkänd."
                        ],
                        [
                            "Uppgift 5 ger bara kunder med order eftersom du använt `INNER JOIN`. Byt till `LEFT JOIN` "
                            + "och kontrollera mot `IS NULL`. Lämna in igen.",
                            "Uppgift 9 filtrerar summan i `WHERE`, vilket inte går. Flytta villkoret till `HAVING` "
                            + "och komplettera.",
                            "Uppgift 12 saknas, och jag ser inget `CREATE TABLE`. Komplettera med din normaliserade "
                            + "modell och förklara normalformerna."
                        ],
                        was: "SQL-övningar")
                ],
                "Relationsdatabaser och SQL"),
            Week(
                "Vecka 4 – EF Core, migrationer och index",
                "Code first, migrationer och prestanda.",
                start.AddDays(21),
                [
                    Lesson(
                        "Teori: Code first och migrationer",
                        """
                        Med **Entity Framework Core** (EF Core) beskriver du databasen som C#-klasser och låter
                        ramverket sköta SQL:en. I **code first** börjar du i koden: klasserna är sanningen, och
                        databasen följer efter. Varje entitet blir en tabell, varje egenskap en kolumn, och
                        relationer uttrycks med navigeringsegenskaper.

                        ```csharp
                        public class Kund
                        {
                            public int Id { get; set; }
                            public required string Namn { get; set; }
                            public List<Order> Ordrar { get; set; } = [];
                        }

                        public class Order
                        {
                            public int Id { get; set; }
                            public int KundId { get; set; }
                            public Kund Kund { get; set; } = null!;
                            public DateTimeOffset Skapad { get; set; }
                        }

                        public class ButikDb(DbContextOptions<ButikDb> options) : DbContext(options)
                        {
                            public DbSet<Kund> Kunder => Set<Kund>();
                            public DbSet<Order> Ordrar => Set<Order>();

                            protected override void OnModelCreating(ModelBuilder modelBuilder) =>
                                modelBuilder.Entity<Kund>().HasIndex(k => k.Namn);
                        }
                        ```

                        När modellen ändras behöver databasen följa med, och det är vad **migrationer** är till
                        för. En migration är en C#-fil som beskriver hur databasen går från ett läge till nästa,
                        med `Up` för framåt och `Down` för bakåt. Du skapar en med `dotnet ef migrations add LäggTillSkapad`
                        och kör den med `dotnet ef database update`. Migrationerna ska ligga i Git, för de är en
                        del av koden.

                        ```bash
                        dotnet ef migrations add LäggTillOrder
                        dotnet ef database update
                        dotnet ef database update 20261001_Init     # gå tillbaka
                        dotnet ef migrations script --idempotent    # SQL för produktion
                        ```

                        Läs alltid den genererade migrationen innan du kör den. EF Core gissar, och ett omdöpt fält ser
                        för EF Core ut som "ta bort kolumnen och skapa en ny", vilket tar bort datan. I produktion
                        genererar man ofta ett SQL-skript eller kör migrationerna från pipelinen i stället för att låta
                        applikationen migrera sig själv vid start.

                        ## Migrationer i praktiken

                        Redigera aldrig en migration som redan körts på någon annans databas. Skapa en ny. Och
                        ändra inte i databasen för hand utan att göra det via en migration, annars hamnar koden och
                        databasen ur takt och nästa kollega får fel.
                        """,
                        "Code first och migrationer"),
                    Lesson(
                        "Teori: Index och prestanda",
                        """
                        Ett **index** är en sorterad struktur (oftast ett B-träd) som databasen kan slå upp i
                        i stället för att läsa hela tabellen. Det är samma tanke som sakregistret i en bok. Utan
                        index gör databasen en **sekventiell skanning** av varje rad, vilket går fint med hundra
                        rader och inte alls med hundra miljoner.

                        Index är inte gratis. Varje index tar plats och måste uppdateras vid varje `INSERT`,
                        `UPDATE` och `DELETE`, så för många index gör skrivningar långsamma. Lägg index på kolumner
                        du söker, sorterar eller joinar på: främmande nycklar (PostgreSQL skapar inte index för dem
                        automatiskt), kolumner i `WHERE` och kolumner i `ORDER BY`. Ett index på en kolumn med få
                        olika värden, som `aktiv` sant eller falskt, hjälper sällan.

                        ```sql
                        CREATE INDEX ix_ordrar_kund_id ON ordrar (kund_id);
                        CREATE INDEX ix_ordrar_kund_skapad ON ordrar (kund_id, skapad DESC);

                        EXPLAIN ANALYZE
                        SELECT * FROM ordrar WHERE kund_id = 42 ORDER BY skapad DESC LIMIT 10;
                        ```

                        Med `EXPLAIN` visar databasen sin **query plan**, och `EXPLAIN ANALYZE` kör frågan och visar
                        verklig tid. Leta efter `Seq Scan` på stora tabeller (ofta ett tecken på saknat index),
                        `Index Scan` eller `Index Only Scan` (bra) och stora skillnader mellan beräknade och faktiska
                        rader. Ett sammansatt index på `(kund_id, skapad)` används för filter på `kund_id`, men
                        inte för filter på bara `skapad`: ordningen på kolumnerna spelar roll.

                        I EF Core lägger du index med `HasIndex` i `OnModelCreating` eller med attributet `[Index]`. Ett
                        vanligt prestandaproblem i EF Core är **N+1**: du hämtar hundra kunder och går sedan
                        i en loop och hämtar ordrarna för var och en, så det blir hundra och en frågor.
                        Ladda relaterad data i förväg med `Include`, eller projicera bara det du behöver med `Select`.

                        ```csharp
                        var kunder = await db.Kunder
                            .Where(k => k.Ordrar.Any())
                            .Select(k => new { k.Namn, Antal = k.Ordrar.Count })
                            .ToListAsync();
                        ```

                        ## Index i praktiken

                        Mät innan du optimerar. Kör `EXPLAIN ANALYZE` före och efter, och skriv ner siffrorna. Att lägga
                        till ett index "för säkerhets skull" är gissning, och en gissning som sänker skrivhastigheten.
                        Slå även på loggning av SQL i EF Core (`LogTo(Console.WriteLine)`) så ser du vilka frågor
                        som verkligen skickas.
                        """,
                        "Index och prestanda"),
                    Exercise(
                        "Övningsuppgift: Läs en query plan",
                        """
                        För att öva på index och query planer ska vi utgå från en tabell med en miljon ordrar. Skriptet
                        `generera_ordrar.sql` i kursmaterialet fyller den åt dig, och det tar någon minut.

                        Kör följande och spara utskriften:

                        ```text
                        > psql butik
                        butik=# EXPLAIN ANALYZE SELECT * FROM ordrar WHERE kund_id = 4711;
                         Seq Scan on ordrar  (cost=0.00..18334.00 rows=100 width=24) (actual time=0.021..61.4 rows=104 loops=1)
                           Filter: (kund_id = 4711)
                           Rows Removed by Filter: 999896
                         Execution Time: 61.5 ms
                        ```

                        Målet är att du ska kunna förklara vad planen säger, och se skillnaden mot en fråga som
                        har ett index. Gör så här:

                        - Skapa ett index på `kund_id` och kör samma `EXPLAIN ANALYZE`. Vad ändras i planen
                          och hur mycket i tid?
                        - Prova en fråga som filtrerar på `skapad` men inte på `kund_id`. Används indexet? Varför?
                        - Skapa ett sammansatt index på `(kund_id, skapad)` och jämför igen.
                        - Kör `INSERT` av tiotusen rader med och utan indexen och jämför tiden.

                        Skriv ner svaren i några rader och diskutera med en kurskamrat. Visualiserar du planen i
                        ett webbverktyg (sök på "explain.dalibo.com") blir det lättare att se var tiden går.
                        Ingen inlämning, men det här kommer på tentan.
                        """),
                    Assignment(
                        "Laboration: Dataåtkomst med EF Core",
                        """
                        I den här laborationen bygger du ett repository ovanpå EF Core för ett eget domänproblem. Det kan
                        vara ett bibliotek, en matkasse, ett träningsdagbok, något du själv är nyfiken på, så länge det
                        har minst tre entiteter med relationer mellan sig. Databasen är PostgreSQL, och du kör den
                        gärna lokalt med Docker.

                        Projektet ska konfigureras så att följande uppfylls:

                        - En `DbContext` med minst tre entiteter, och minst en ett-till-många- och en
                          många-till-många-relation.
                          - Främmande nycklar, obligatoriska fält och maxlängder ska uttryckas i modellen.
                          - Minst en egen regel, till exempel `CHECK` eller unikt index.
                        - Minst ==två migrationer== som ligger i Git: den första skapar schemat och den andra är en
                          ändring i modellen. Visa den genererade SQL:en (`dotnet ef migrations script`).
                        - Ett repository (`IKundRepository` eller liknande) med asynkrona metoder för att skapa, hämta,
                          uppdatera och ta bort.
                        - Minst en fråga där du valt att lägga ett ==index==, med `EXPLAIN ANALYZE` före och efter.
                        - Minst en operation som kräver en transaktion, och du ska visa vad som händer vid fel
                          (rollback).

                        Jag vill se att du kan visa att något fungerar: skriv tester, eller ett litet program som
                        visar flödet, och spara utskriften. Jämför också antalet SQL-frågor för en lista med
                        relaterad data med och utan `Include`.

                        💡 Tips

                        Börja litet med två entiteter och få migrationer att fungera, lägg sedan till resten. Det går
                        att nollställa en lokal databas med `dotnet ef database drop`, men gör det aldrig mot en
                        databas som någon annan använder.

                        Läs migrationen innan du kör den. EF Core gissar ibland fel, särskilt vid namnbyten.

                        ## Att lämna in

                        - [ ] Länk till repot med migrationerna incheckade
                        - [ ] Utskrift av `EXPLAIN ANALYZE` före och efter ditt index
                        - [ ] En beskrivning av transaktionen och vad som händer vid rollback
                        - [ ] En kort motivering av modellen och dina designval

                        **Betygskriterier**

                        För Godkänt ska alla krav ovan vara uppfyllda. För Väl godkänt ska du dessutom motivera
                        modellen och indexvalet med mätningar, visa att du undviker N+1-frågor och hantera
                        samtidiga ändringar (till exempel med en `RowVersion`). Laborationen examinerar kursmål 3
                        (transaktioner), kursmål 4 (index och query planer) och kursmål 5 (EF Core, code first och
                        migrationer).
                        """,
                        Friday(start, 5),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Jag har modellerat en träningsdagbok med `Pass`, `Övning` och `PassÖvning` (många-till-många). "
                            + "Två migrationer: `Init` och `LäggTillAnteckning`. Ett index på `Pass.Datum` tog en "
                            + "fråga från 48 ms till 0,4 ms enligt `EXPLAIN ANALYZE`. Transaktionen sparar ett "
                            + "pass med alla sina övningar, och vid fel rullas allt tillbaka.",
                            "Repositoryt `IReceptRepository` har fem asynkrona metoder. Jag hade först N+1-problemet i "
                            + "listan över recept och ingredienser: 101 frågor. Med `Include` blev det en. Jag lade "
                            + "ett unikt index på receptets namn.",
                            "Domänen är ett bibliotek med `Bok`, `Författare` och `Lån`. Jag har en migration för "
                            + "schemat och en där jag lade till `Isbn` med unikt index. Jag glömde först att kolumnen "
                            + "var obligatorisk och fick ett fel vid migreringen av befintlig data.",
                            "Jag valde att göra en matkasse. Transaktionen är `SkapaBeställning`, som minskar lagret "
                            + "och skapar en order. Jag testade att kasta ett undantag mitt i och då var lagret "
                            + "oförändrat. Indexet på `OrderRad.OrderId` gjorde frågan snabbare.",
                            "Vi har bara en migration eftersom vi ändrade modellen innan första körningen. Jag vet "
                            + "att kravet är två och skriver till om det."
                        ],
                        [
                            "Mycket bra: modellen är genomtänkt, du visar siffror före och efter indexet och "
                            + "transaktionstestet bevisar att rollbacken fungerar. Det lyfter till VG.",
                            "Godkänd. Migrationerna är tydliga och du lade märke till N+1. Lägg gärna till en "
                            + "`RowVersion` nästa gång för samtidiga ändringar.",
                            "Godkänd. Bra att du läste den genererade SQL:en. Indexet är motiverat med en mätning."
                        ],
                        [
                            "Du har bara en migration, och kravet är två. Gör en ändring i modellen, skapa en "
                            + "ny migration och lämna in igen.",
                            "Jag saknar en `EXPLAIN ANALYZE` före och efter indexet. Utan mätning vet vi inte om "
                            + "indexet hjälper. Komplettera.",
                            "Transaktionen saknar en visad rollback. Visa vad som händer när något går fel mitt i, "
                            + "till exempel med ett test som kastar ett undantag."
                        ],
                        was: "Dataåtkomst med EF Core")
                ],
                "EF Core och PostgreSQL")
        ],
        DatabasesNews(start, author));

    private static NewsSpec[] DatabasesNews(DateOnly start, string author) =>
    [
        new(
            "Välkommen till Databaser",
            """
            Välkommen till kursen **Databaser**. Under de första veckorna lär vi oss tänka i tabeller, skriva SQL och
            skydda datan med transaktioner. Sedan flyttar vi tillbaka till C# och använder EF Core mot PostgreSQL,
            med migrationer och index.

            Installera PostgreSQL (eller kör den med Docker) och en SQL-klient som pgAdmin eller DBeaver före första
            lektionen. Övningsdatabasen och skriptet till den första laborationen ligger i kursmaterialet.
            """,
            Se(start.AddDays(-3), 14),
            author,
            true,
            true,
            [
                new("#2", "Går det bra att använda MySQL i stället, jag har det installerat sedan förra året?", 3),
                new(author, "Nej, vi kör PostgreSQL hela kursen, eftersom EF Core-delen och det mesta av SQL:en "
                    + "bygger på den. Det är samma sak med Docker: `docker run postgres` räcker.", 5),
                new("#9", "Jag kör Docker, det tog fem minuter. Jag lägger upp min compose-fil i chatten.", 8)
            ]),
        new(
            "Fråga om NULL: varför får jag inga rader?",
            """
            En fråga som dyker upp varje år, så jag lägger den här där alla kan se. Skriv gärna dina egna
            frågor om SQL i kommentarerna, så svarar vi här i stället för i mejl.

            Fråga: *"Jag försöker hitta kunder utan telefonnummer med `WHERE telefon = NULL` men får inga rader,
            trots att jag vet att det finns några. Vad gör jag fel?"*
            """,
            Se(start.AddDays(4), 11),
            author,
            false,
            true,
            [
                new("#5", "Jag tror att det är för att NULL inte är lika med någonting, inte ens med NULL. Man "
                    + "ska skriva `WHERE telefon IS NULL`.", 2),
                new("#11", "Jo, och `<> NULL` fungerar inte heller. Det blev jag också biten av.", 3),
                new(author, "Precis, #5 har rätt. `NULL` betyder okänt, och en jämförelse med något okänt är "
                    + "också okänd, så raden tas aldrig med. Använd `IS NULL` och `IS NOT NULL`. Behöver du ett "
                    + "alternativt värde i resultatet finns `COALESCE(telefon, 'saknas')`.", 5),
                new("#5", "Och `count(telefon)` räknar inte med de som saknar telefon, medan `count(*)` gör det.", 6),
                new(author, "Bra tillägg. Det är en klassiker på tentan.", 7)
            ]),
        new(
            "Hjälpträff inför EF Core-laborationen",
            """
            Nästa fredag lämnas **Dataåtkomst med EF Core** in. Jag har en hjälpträff på onsdag kl. 15.00 i sal B204.
            Ta med projektet så tittar vi på migrationer, relationer och hur man läser en query plan. Har du fastnat på
            en migration som inte vill köras, ta med felmeddelandet.

            Kom ihåg att lägga in migrationerna i Git och att ni visar `EXPLAIN ANALYZE` före och efter indexet.
            """,
            Se(start.AddDays(28), 10),
            author,
            false,
            true,
            [
                new("#7", "Min migration säger att tabellen redan finns. Är det något jag kan fixa innan onsdag?", 2),
                new(author, "Det betyder oftast att databasen skapats på något annat sätt än via migrationer. Ta bort "
                    + "databasen (`dotnet ef database drop`) och kör `database update` igen, så brukar det gå. Kommer "
                    + "det tillbaka tar vi det på onsdag.", 4),
                new("#7", "Det löste sig, tack!", 6),
                new("#13", "Kan man använda SQLite i stället för PostgreSQL för labben?", 9),
                new(author, "Nej, vi vill se PostgreSQL, bland annat för att `EXPLAIN` ser annorlunda ut i SQLite.", 11)
            ]),
        new(
            "Tentan: ändrad sal",
            """
            Ändring inför tentan på onsdag: den skrivs kl. 08.30 men i **sal C112** i stället för B204. Ta med
            ID-handling. Hjälpmedel är en handskriven A4 (båda sidor), men ingen dator.

            Tentan har tre delar: SQL-frågor på papper, ett par frågor om normalisering och transaktioner, och
            en uppgift om att läsa en query plan. Samtliga delar finns med i övningsmaterialet.
            """,
            Se(start.AddDays(38), 9),
            author,
            true,
            true,
            [
                new("#1", "Får man ha med SQL-spickzettel från nätet utskriven?", 2),
                new(author, "Bara det du skrivit för hand på din egen A4. Utskrifter räknas inte.", 4),
                new("#6", "Kommer det vara en uppgift med en egen `CREATE TABLE`?", 7),
                new(author, "Ja, normaliseringsfrågan går ut på att rita upp tabeller med nycklar. Öva på den.", 9)
            ]),
        new(
            "Så gick tentan",
            """
            Tentan är rättad. En stor majoritet klarade den, och SQL-delen gick över förväntan. Det som oftast
            blev fel var **transaktioner** (många glömde vad `ROLLBACK` gör) och query planen: flera tyckte
            att ett `Seq Scan` var bra. Det är det inte på en stor tabell.

            Resultaten finns i kursrummet. Det finns en omtenta i januari, och ni som vill gå igenom er tenta
            kan boka en tid med mig.
            """,
            Se(start.AddDays(45), 14),
            author,
            false,
            true,
            [
                new("#4", "Tack! Går det bra att få se vilka poäng man fick på varje fråga?", 3),
                new(author, "Ja, poängen per fråga står i bedömningen i kursrummet.", 4),
                new("#10", "Jag hade missat 3NF om jag inte läst om den kvällen innan. Tack för bra kurs!", 6),
                new(author, "Tack själv, och bra jobbat allihop.", 8)
            ])
    ];
}
