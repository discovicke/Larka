using Backend.Domain.Enums;
using static Backend.Infrastructure.Persistence.Seeding.DemoPeople;

namespace Backend.Infrastructure.Persistence.Seeding;

internal static partial class DemoCatalog
{
    private static CourseSpec WebApps(string prefix, DateOnly start, DateOnly end, string author) => new(
        $".{prefix}_WEBB",
        "Webbapplikationer i C#",
        "Webb-API:er med ASP.NET Core och en klient i Vue, med autentisering och ett teamprojekt.",
        40,
        start,
        end,
        author,
        [
            Week(
                "Vecka 1 – ASP.NET Core Web API, routing och JWT",
                "Routing, validering och JWT.",
                start,
                [
                    Lesson(
                        "Teori: Minimal API:er och routing",
                        """
                        En webbapplikation består i grunden av en server som tar emot HTTP-anrop och svarar med data
                        eller HTML. I ASP.NET Core finns två sätt att skriva ett API: controllers och minimal API:er.
                        I kursen använder vi minimal API:er, där man mappar en metod och en sökväg direkt i
                        `Program.cs`. Det ger mindre ceremoni, men det betyder inte att allt får ligga i en enda fil.

                        Varje endpoint har en HTTP-metod, en route och en handler. `GET` hämtar, `POST` skapar, `PUT`
                        ersätter, `PATCH` ändrar delar och `DELETE` tar bort. Routen kan innehålla parametrar, till
                        exempel `/api/bookings/{id}`, som ASP.NET Core binder till parametrar i handlern. Tjänster som
                        databaskontext och egna klasser hämtas från DI-containern genom att man helt enkelt skriver
                        dem som parametrar.

                        Håll endpoints tunna. En endpoint ska läsa indata, anropa en tjänst och översätta resultatet
                        till ett HTTP-svar. Affärsreglerna hör hemma i tjänsten, annars blir de omöjliga att testa
                        utan att starta hela servern.

                        ```csharp
                        var builder = WebApplication.CreateBuilder(args);
                        builder.Services.AddScoped<BookingService>();
                        var app = builder.Build();

                        var bookings = app.MapGroup("/api/bookings");

                        bookings.MapGet("/", async (BookingService service) =>
                            Results.Ok(await service.ListAsync()));

                        bookings.MapGet("/{id:int}", async (int id, BookingService service) =>
                            await service.FindAsync(id) is { } booking
                                ? Results.Ok(booking)
                                : Results.NotFound());

                        bookings.MapPost("/", async (CreateBooking request, BookingService service) =>
                        {
                            var booking = await service.CreateAsync(request);
                            return Results.Created($"/api/bookings/{booking.Id}", booking);
                        });

                        app.Run();
                        ```

                        ## Routing och statuskoder i praktiken

                        Använd `MapGroup` för att samla endpoints under samma prefix, och lägg gemensamma saker som
                        auktorisering på gruppen i stället för på varje endpoint. Returnera alltid en statuskod som
                        stämmer med vad som hänt: `200` när något hämtats, `201` med en `Location`-header när något
                        skapats, `204` när det inte finns något att skicka tillbaka, `400` när indata är fel, `404`
                        när resursen inte finns och `409` när anropet krockar med något som redan finns.

                        Ett vanligt misstag är att svara `200` med texten "fel" i kroppen. Klienten ska kunna förlita
                        sig på statuskoden, så att den inte behöver tolka texten. Det är också statuskoden som
                        lärarna tittar på först när vi testar era API:er.
                        """,
                        "Minimal API:er och routing"),
                    Lesson(
                        "Teori: Validering och felhantering",
                        """
                        Allt som kommer utifrån ska betraktas som opålitligt. Ett API som litar på att klienten
                        skickar rätt blir förr eller senare en källa till konstiga buggar och, i värsta fall,
                        säkerhetshål. Validera därför indata på servern även om klienten också gör det. Klientens
                        validering finns för användarens skull, serverns för systemets.

                        Det enklaste är att validera själv i tjänsten eller med ett litet bibliotek som
                        FluentValidation. Skriv reglerna som kod som går att testa: sluttiden måste ligga efter
                        starttiden, rummet måste finnas, ett rum får inte vara dubbelbokat. När en regel bryts
                        svarar du med `400` (eller `409` vid krock) och en förklaring som en människa kan läsa.

                        Standardformatet för felsvar heter **ProblemDetails** (RFC 9457). Det ger klienten ett
                        förutsägbart JSON-svar med `title`, `status` och `detail`, och för valideringsfel en lista
                        över vilka fält som är fel.

                        ```csharp
                        builder.Services.AddProblemDetails();
                        app.UseExceptionHandler();
                        app.UseStatusCodePages();

                        bookings.MapPost("/", async (CreateBooking request, BookingService service) =>
                        {
                            if (request.End <= request.Start)
                            {
                                return Results.ValidationProblem(new Dictionary<string, string[]>
                                {
                                    ["end"] = ["Sluttiden måste ligga efter starttiden."]
                                });
                            }

                            var result = await service.CreateAsync(request);
                            return result is null
                                ? Results.Problem("Rummet är redan bokat då.", statusCode: 409)
                                : Results.Created($"/api/bookings/{result.Id}", result);
                        });
                        ```

                        ## Fel som användaren aldrig ska se

                        Ett oväntat undantag ska ge `500` och en generisk text, aldrig en stack trace. Logga
                        detaljerna på servern i stället. Läs gärna loggen efteråt, där finns oftast svaret på varför
                        något gick fel.
                        """),
                    Lesson(
                        "Teori: Autentisering med JWT",
                        """
                        Autentisering handlar om vem du är, auktorisering om vad du får göra. Ett vanligt sätt att
                        lösa det i ett API är **JWT** (JSON Web Token). Användaren loggar in med e-post och lösenord,
                        servern kontrollerar uppgifterna och svarar med ett signerat token. Klienten skickar sedan
                        tokenet i `Authorization`-headern vid varje anrop: `Authorization: Bearer <token>`.

                        Ett JWT består av tre delar, header, payload och signatur, som är base64-kodade och
                        separerade med punkter. Payloaden innehåller **claims**, till exempel användarens id, roll
                        och när tokenet går ut. Tänk på att payloaden går att läsa för alla som har tokenet. Den är
                        signerad, inte krypterad, så lägg aldrig lösenord eller personuppgifter i den.

                        Hur länge ska ett token gälla? Kort, helst en timme eller mindre. Ett token som läcker kan
                        inte återkallas innan det går ut, så ju kortare livslängd desto mindre skada. Lösenord ska
                        lagras som hash med en långsam algoritm (till exempel bcrypt eller `PasswordHasher` i
                        ASP.NET Core Identity), aldrig som klartext och aldrig med ett vanligt SHA-hash.

                        ```csharp
                        builder.Services
                            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                            .AddJwtBearer(options =>
                            {
                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidateAudience = true,
                                    ValidateLifetime = true,
                                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                                    ValidAudience = builder.Configuration["Jwt:Audience"],
                                    IssuerSigningKey = new SymmetricSecurityKey(
                                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
                                };
                            });
                        builder.Services.AddAuthorization();

                        app.UseAuthentication();
                        app.UseAuthorization();

                        bookings.RequireAuthorization();
                        app.MapDelete("/api/rooms/{id:int}", DeleteRoom)
                           .RequireAuthorization(p => p.RequireRole("Admin"));
                        ```

                        ## Hemligheter och roller

                        Signeringsnyckeln är en hemlighet. Lägg den i användarhemligheter (`dotnet user-secrets`)
                        lokalt och i en miljövariabel i produktion, aldrig i `appsettings.json` som checkas in. Med
                        roller kan du skilja på vanliga användare och administratörer: `401` betyder att du inte är
                        inloggad, `403` att du är inloggad men inte får göra det här.
                        """,
                        "Autentisering med JWT"),
                    Exercise(
                        "Övningsuppgift: Skydda en endpoint med JWT",
                        """
                        För att öva på autentisering utgår vi från ett litet API med två endpoints:

                        ```
                        POST /api/auth/login   { "email": "...", "password": "..." }  -> { "token": "..." }
                        GET  /api/secret                                              -> 200 med hemlig text
                        ```

                        Målet är att `GET /api/secret` ska svara `401` utan token, `200` med ett giltigt token och
                        `401` igen när tokenet har gått ut. Börja med att hårdkoda en användare i koden. Det är inte
                        något du ska lämna kvar, men det gör att du kan fokusera på hur tokenet skapas och
                        kontrolleras.

                        Pröva dig fram med en `.http`-fil i Visual Studio eller Rider (eller Postman om du föredrar
                        det), och gör följande:

                        - logga in och kopiera tokenet
                        - klistra in det på https://jwt.ms och titta på vilka claims som finns
                        - skicka anropet med och utan `Authorization`-headern
                        - sätt livslängden till en minut, vänta, och se vad som händer

                        Ingen inlämning. Fastnar du får du ställa frågor på handledningen.
                        """),
                    Assignment(
                        "Laboration: REST-API för bokningar",
                        """
                        I den här laborationen bygger du ett API för bokning av rum, som ett företag skulle kunna
                        använda för sina mötesrum. Du får själv välja om du använder en databas (EF Core med
                        PostgreSQL eller SQLite rekommenderas) eller håller data i minnet under utvecklingen, men
                        slutresultatet ska klara att startas om utan att bokningarna försvinner.

                        Använd minimal API:er och gärna en `MapGroup` per resurs. Bibliotek för validering och
                        testning får du välja själv. Vi rekommenderar xUnit och `WebApplicationFactory` för
                        testerna.

                        API:et ska uppfylla följande:

                        - Rum och bokningar
                          - `GET /api/rooms` listar alla rum
                          - `GET /api/bookings` listar den inloggade användarens bokningar
                          - `POST /api/bookings` skapar en bokning med rum, starttid och sluttid
                          - `DELETE /api/bookings/{id}` tar bort en bokning, men bara om den är din
                        - Validering och felhantering
                          - sluttiden måste ligga efter starttiden, annars `400` med ProblemDetails
                          - ett rum får inte bokas två gånger på samma tid, annars `409`
                          - ett okänt rum eller en okänd bokning ger `404`
                        - Autentisering
                          - `POST /api/auth/login` returnerar ett JWT med användarens id och roll som claims
                          - alla endpoints utom inloggningen kräver token, annars `401`
                          - endast rollen `Admin` får skapa och ta bort rum, annars `403`
                          - signeringsnyckeln får inte ligga i repot

                        Det ska gå att se att det fungerar. Lägg därför en `.http`-fil i repot med ett anrop per
                        punkt ovan, både de som lyckas och de som ska misslyckas. Skriv också minst fem
                        integrationstester som visar att dubbelbokning ger `409` och att anrop utan token ger `401`.
                        Testa gärna gränsfall, som en bokning som slutar precis när nästa börjar. Den ska vara
                        tillåten.

                        ==Statuskoderna är en del av API:ets kontrakt==, så var noga med att de stämmer med listan
                        ovan.

                        💡 Tips

                        Börja med rummen och bokningarna utan inloggning, och lägg på JWT när allt annat fungerar.
                        Det är mycket lättare att felsöka i den ordningen.

                        Dubbelbokning kontrollerar du i tjänsten, inte i endpointen. Två bokningar överlappar om
                        `a.Start < b.End && b.Start < a.End`, och det är bra att skriva ett enhetstest för just den
                        raden.

                        ## Att lämna in

                        - [ ] Länk till repot (se till att vi har läsrättigheter)
                        - [ ] En kort README med hur man startar API:et och en tabell över endpoints och statuskoder
                        - [ ] En redovisning i svaret: hur ni validerar, hur JWT:t skapas och vad ni valde att inte göra

                        **Betygskriterier**

                        Godkänt kräver att alla punkter ovan är uppfyllda. För väl godkänt ska du dessutom kunna
                        motivera dina val: varför just den tokenlivslängden, hur du lagrar lösenord och hur du
                        hanterar att två personer försöker boka samma rum samtidigt. Laborationen kopplar till
                        kursmål 1 och 3: att utveckla webb-API:er med ASP.NET Core och att skydda dem med
                        autentisering och auktorisering.
                        """,
                        Friday(start, 3),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Vi har byggt API:et med minimal API:er och EF Core mot SQLite. Dubbelbokning kontrolleras "
                            + "i `BookingService.CreateAsync` och ger `409`. JWT:t har en livslängd på 60 minuter och "
                            + "innehåller `sub` och `role`. Vi har sju integrationstester med WebApplicationFactory.",
                            "Jag löste uppgiften med PostgreSQL i Docker. Det svåraste var överlappskontrollen, "
                            + "först missade jag fallet där en bokning slutar exakt när nästa börjar. Det har jag nu "
                            + "ett eget test för. Signeringsnyckeln ligger i user-secrets och README förklarar hur "
                            + "man sätter den.",
                            "Alla endpoints finns och `.http`-filen visar både lyckade och misslyckade anrop. "
                            + "Lösenorden lagras med `PasswordHasher`. Jag valde att inte göra refresh-tokens eftersom "
                            + "uppgiften inte ber om det, men jag beskriver i README hur det skulle kunna läggas till.",
                            "Klart. `DELETE /api/bookings/{id}` svarar `404` om bokningen tillhör någon annan, så "
                            + "att man inte kan se att den finns. Jag vet att `403` också hade varit rimligt och "
                            + "motiverar mitt val i README.",
                            "Här är vårt API. Vi delade upp det så att jag gjorde inloggningen och min kurskamrat "
                            + "bokningarna. Vi har fem tester och en README. Felsvaren följer ProblemDetails."
                        ],
                        [
                            "Godkänd. Dubbelbokningen är rätt löst och testad, och `.http`-filen gjorde det lätt "
                            + "att provköra. Tänk på att flytta valideringen av tiderna till tjänsten så att den "
                            + "kan testas utan HTTP.",
                            "Väl godkänd. Du motiverar `404` kontra `403` på ett sätt som visar att du förstått "
                            + "säkerhetsaspekten, och gränsfallet med slut och start på samma minut är testat.",
                            "Godkänd. Alla statuskoder stämmer och JWT-hanteringen är ordentlig. För VG hade jag "
                            + "velat se en motivering till tokenets livslängd."
                        ],
                        [
                            "Du är nästan i mål, men `POST /api/bookings` svarar `200` i stället för `201`, och "
                            + "överlappande bokningar släpps igenom när sluttiden är lika med en annan boknings "
                            + "starttid. Rätta de två sakerna, lägg till ett test och lämna in igen.",
                            "API:et fungerar, men signeringsnyckeln ligger i `appsettings.json` i repot. Flytta den "
                            + "till user-secrets eller en miljövariabel, byt nyckel och beskriv det i README.",
                            "Redovisningen saknas i svaret och endast två endpoints har skydd mot `401`. Skydda "
                            + "resten och skriv några rader om hur ni validerar indata."
                        ],
                        "REST-API för bokningar")
                ],
                "ASP.NET Core Web API"),
            Week(
                "Vecka 5 – Frontend med Vue, komponenter och API-anrop",
                "Komponenter, reaktivitet och API-anrop.",
                start.AddDays(28),
                [
                    Lesson(
                        "Teori: HTML, CSS och JavaScript som grund",
                        """
                        Innan vi använder ett ramverk är det värt att påminna sig om vad webbläsaren faktiskt får.
                        Servern skickar HTML (struktur), CSS (utseende) och JavaScript (beteende). Ett ramverk som
                        Vue bygger ovanpå det, men det ersätter det inte. Förstår du inte `display: flex`, en
                        `<label>` eller hur en händelse bubblar kommer du att kämpa i ramverket också.

                        Skriv semantisk HTML. Använd `<button>` för knappar, `<nav>`, `<main>` och `<form>` för det de
                        är, och koppla alltid en `<label>` till varje fält. Det är inte bara snyggt: skärmläsare och
                        tangentbordsanvändare är beroende av det, och tillgänglighet är ett krav i offentlig sektor,
                        där många av er kommer att arbeta. Pröva att använda din egen sida med bara tangentbordet.

                        ```html
                        <form id="booking-form">
                          <label for="room">Rum</label>
                          <select id="room" name="room" required></select>

                          <label for="start">Start</label>
                          <input id="start" name="start" type="datetime-local" required />

                          <button type="submit">Boka</button>
                        </form>
                        ```

                        ## Fetch utan ramverk

                        Webbläsarens `fetch` ger ett löfte (`Promise`). Kom ihåg att `fetch` inte kastar fel vid
                        `404` eller `500`, bara när nätverket är nere, så du måste själv titta på `response.ok`.
                        Vi ser lite längre fram hur man gör det på ett ställe i stället för vid varje anrop.
                        """),
                    Lesson(
                        "Teori: Komponenter och reaktivitet",
                        """
                        Vue bygger gränssnittet av **komponenter**, små bitar som kapslar in mall, logik och
                        stil. En bokningslista kan bestå av komponenterna `BookingList`, `BookingRow` och
                        `BookingForm`. Komponenter tar emot data via **props** och talar om att något har hänt via
                        **events** (`emit`). Data går alltså neråt och händelser uppåt, och det gör det lätt att
                        följa vad som påverkar vad.

                        Tillståndet håller du i reaktiva variabler. `ref` skapar ett värde som Vue bevakar, och när
                        det ändras uppdateras de delar av sidan som använder det. `computed` räknar ut ett värde
                        från andra och cachar resultatet, och `watch` låter dig reagera på en förändring, till
                        exempel hämta ny data när ett filter ändras.

                        ```vue
                        <script setup lang="ts">
                        import { ref, computed } from 'vue'

                        const props = defineProps<{ bookings: Booking[] }>()
                        const emit = defineEmits<{ cancel: [id: number] }>()

                        const onlyFuture = ref(true)
                        const visible = computed(() =>
                          props.bookings.filter(b => !onlyFuture.value || new Date(b.start) > new Date()))
                        </script>

                        <template>
                          <label><input type="checkbox" v-model="onlyFuture" /> Bara kommande</label>
                          <ul>
                            <li v-for="b in visible" :key="b.id">
                              {{ b.roomName }} {{ b.start }}
                              <button @click="emit('cancel', b.id)">Avboka</button>
                            </li>
                          </ul>
                        </template>
                        ```

                        ## Komponenter i praktiken

                        Dela upp efter ansvar, inte efter hur sidan ser ut. Om en komponent både hämtar data,
                        filtrerar, validerar och ritar är den för stor. Ett bra tecken är att du kan beskriva vad en
                        komponent gör i en mening utan ordet "och".
                        """,
                        "Komponenter och reaktivitet"),
                    Lesson(
                        "Teori: Anropa API:et och hantera inloggning",
                        """
                        Klienten pratar med API:et via HTTP. Samla alla anrop på ett ställe, i en liten modul eller
                        ett composable, så att du bara behöver lägga till `Authorization`-headern och hantera
                        felen en gång. Visa alltid tre lägen för användaren: laddar, fel och klart. En sida som
                        stannar tyst när API:et inte svarar är värre än en som säger att något gick fel.

                        ```ts
                        export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
                          const token = sessionStorage.getItem('token')
                          const response = await fetch(`/api${path}`, {
                            ...init,
                            headers: {
                              'Content-Type': 'application/json',
                              ...(token ? { Authorization: `Bearer ${token}` } : {}),
                              ...init.headers
                            }
                          })

                          if (response.status === 401) {
                            sessionStorage.removeItem('token')
                            router.push('/login')
                          }
                          if (!response.ok) {
                            throw new ApiError(response.status, await response.json().catch(() => null))
                          }
                          return response.status === 204 ? (undefined as T) : response.json()
                        }
                        ```

                        ## Var ska tokenet ligga?

                        Det finns inget perfekt svar. `localStorage` överlever att fliken stängs men kan läsas av
                        skadlig JavaScript om sidan har en XSS-sårbarhet. En cookie med `HttpOnly` är säkrare men
                        kräver att ni tänker på CSRF. I kursen duger `sessionStorage`, men skriv i redovisningen
                        vilken risk ni accepterar, och se till att aldrig skriva ut tokenet i konsolen.

                        Vid utveckling kör klienten och API:et ofta på olika portar och då blockerar webbläsaren
                        anropen. Lös det med en proxy i Vite (`server.proxy`) eller genom att aktivera CORS för
                        just er klients adress i API:et, aldrig `AllowAnyOrigin` tillsammans med inloggning.
                        """,
                        "Anropa API:et"),
                    Exercise(
                        "Övningsuppgift: En lista med laddning och fel",
                        """
                        Bygg en liten Vue-komponent som hämtar rummen från `GET /api/rooms` och visar dem i en lista.
                        Den ska hantera tre lägen:

                        - medan anropet pågår visas texten "Laddar rum…"
                        - om anropet misslyckas visas ett felmeddelande och en knapp "Försök igen"
                        - annars visas listan, och en tom lista ger texten "Inga rum ännu"

                        Stäng av servern och kontrollera att felläget fungerar, och sätt gärna en fördröjning på
                        servern (`await Task.Delay(2000)`) för att se laddningsläget. Det är lätt att glömma att
                        testa de lägena eftersom de sällan syns när allt fungerar.

                        Ingen inlämning, men det här är i princip skelettet till nästa laboration.
                        """),
                    Assignment(
                        "Laboration: Klient mot ert API",
                        """
                        Nu bygger du en klient till API:et från förra laborationen. Du får gärna fortsätta på ditt
                        eget API, men det går också bra att använda en kurskamrats om du meddelar mig i
                        redovisningen. Klienten ska byggas i Vue med Vite, och du väljer själv om du använder
                        TypeScript, Pinia eller ett komponentbibliotek. Vi rekommenderar TypeScript, och att ni
                        inte lägger tid på ett omfattande komponentbibliotek.

                        Klienten ska uppfylla följande:

                        - Inloggning
                          - ett formulär med e-post och lösenord som anropar `POST /api/auth/login`
                          - tokenet sparas på ett ställe och skickas med i alla anrop
                          - utloggning rensar tokenet, och en användare utan token skickas till inloggningen
                        - Bokningar
                          - en sida som listar användarens bokningar
                          - ett formulär för att skapa en bokning, med val av rum och tider
                          - möjlighet att avboka, med en bekräftelse innan det sker
                        - Felhantering
                          - laddning och fel visas för användaren i alla vyer
                          - valideringsfel från API:et (`400`/`409`) visas intill rätt fält eller som ett tydligt
                            meddelande, inte som en rå JSON-sträng
                          - `401` från API:et skickar användaren till inloggningen

                        Lägg minst två komponenter med egen logik under test, till exempel formuläret och listan,
                        med Vitest och Vue Test Utils. Testa att felmeddelandet visas när API:et svarar `409`, och
                        att knappen är avstängd medan anropet pågår.

                        Sidan ska fungera med tangentbordet och alla fält ska ha en `<label>`. ==Kontrollera själv
                        med Lighthouse eller axe== och skriv resultatet i redovisningen.

                        💡 Tips

                        Gör först en enda sida som fungerar från inloggning till ny bokning, och lägg till
                        resten sedan. Det är frestande att börja med layouten, men den blir lättare att göra när du
                        vet vilken data som ska visas.

                        Får du CORS-fel? Läs nyheten om det, där finns två sätt att lösa det.

                        ## Att lämna in

                        - [ ] Länk till klientens repo (eller mapp i det gemensamma repot)
                        - [ ] En skärmdump på hur formuläret visar ett valideringsfel från API:et
                        - [ ] En redovisning av var tokenet lagras, vilken risk det innebär och hur ni hanterar `401`

                        **Betygskriterier**

                        Godkänt kräver att inloggning, lista, skapa och avboka fungerar och att fel hanteras enligt
                        kraven. För väl godkänt ska komponenterna vara rimligt uppdelade, testerna visa något
                        meningsfullt och redovisningen visa att du har tänkt på tillgänglighet och säkerhet.
                        Laborationen kopplar till kursmål 2: att utveckla en webbklient som använder ett API.
                        """,
                        Friday(start, 6),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Klienten är byggd med Vue, TypeScript och Pinia. Tokenet ligger i `sessionStorage` och "
                            + "läggs på av en gemensam `api()`-funktion som också skickar användaren till `/login` vid "
                            + "`401`. Jag har tre komponenttester med Vitest. Lighthouse gav 96 i tillgänglighet.",
                            "Vi använde mitt API och min kurskamrats Vue-projekt som utgångspunkt. Valideringsfel från "
                            + "`409` visas under tidsfältet. Det svåraste var CORS, vi löste det med en proxy i Vite. "
                            + "Tokenet ligger i `localStorage`, vilket jag vet är en risk vid XSS, men vi har inga "
                            + "`v-html` någonstans.",
                            "Alla vyer visar laddning och fel. Avboka har en bekräftelsedialog som är en egen "
                            + "komponent med `emit`. Jag hann inte skriva tester för formuläret, bara för listan, "
                            + "och jag redovisar det i README.",
                            "Klienten fungerar mot vårt API. Axe hittade två fält utan label som jag rättade. Tokenet "
                            + "rensas vid utloggning och vid `401`."
                        ],
                        [
                            "Godkänd. Inloggning, lista och avbokning fungerar och felen visas tydligt. Dela upp "
                            + "`BookingView` i fler komponenter nästa gång, den gör för mycket.",
                            "Väl godkänd. Gemensam `api()`-funktion, bra testfall för `409` och en redovisning som "
                            + "visar att du förstått avvägningen kring var tokenet lagras.",
                            "Godkänd. Allt som kraven ber om finns. För VG saknar jag tester av formuläret, bara "
                            + "listan är testad."
                        ],
                        [
                            "Formuläret visar `409` som rå JSON. Läs ut `detail` ur ProblemDetails och visa den texten "
                            + "intill fältet, och lämna sedan in igen.",
                            "Tokenet skickas inte med vid avbokning, så `DELETE` ger `401`. Flytta anropet till den "
                            + "gemensamma funktionen och kontrollera att utloggning fungerar.",
                            "Redovisningen om tokenet och tillgänglighet saknas, och inga komponenter har testats. "
                            + "Komplettera båda delarna."
                        ],
                        "Klient mot ert API")
                ],
                "Frontend med Vue"),
            Week(
                "Vecka 7 – Teamprojekt: en webbapplikation i grupp",
                "Webbapplikation i grupp.",
                start.AddDays(42),
                [
                    Lesson(
                        "Teori: Arbeta tillsammans i ett webbprojekt",
                        """
                        I teamprojektet bygger ni en komplett webbapplikation tillsammans, och det som oftast går
                        snett är inte tekniken utan samarbetet. Sätt därför upp reglerna första dagen: hur ni
                        namnger grenar, vem som granskar vad, vad som gäller för en färdig uppgift och hur ni
                        gör när två personer ändrar samma fil.

                        Dela upp arbetet i vertikala skivor, inte i lager. "Du gör databasen, jag gör klienten"
                        betyder att ingen kan testa något förrän båda är klara. "Du gör bokning, jag gör
                        inloggning" betyder att båda har något som går att köra efter första dagen. Lägg
                        gränssnittet mellan klient och API tidigt, till exempel som ett OpenAPI-dokument, så att
                        ni kan arbeta samtidigt.

                        Håll `main` grön. Alla ändringar går via pull requests som minst en annan person har läst,
                        och pipelinen ska bygga och testa. Det tar tio minuter extra per ändring och sparar
                        timmar vid redovisningen. Skriv också ned i README hur man startar hela systemet med ett
                        kommando, för det är det första vi gör när vi ska bedöma projektet.

                        ## Mötet varje morgon

                        Ett kort stående möte på femton minuter räcker: vad gjorde jag igår, vad gör jag idag, vad
                        hindrar mig. Skriv ned hindren, så att ni vet vad ni ska ta upp på handledningen.
                        """),
                    Assignment(
                        "Laboration: Webbapplikation i grupp",
                        """
                        Gör en komplett webbapplikation i grupp om tre till fyra personer. Ni väljer själva en
                        idé, men den ska vara tillräckligt stor för att alla ska ha något att göra, och tillräckligt
                        liten för att bli klar på två veckor. Några exempel som tidigare grupper valt är en
                        bokningstjänst, en receptsamling med betyg och en enkel ärendehanterare för en förening.
                        Skicka idén till mig innan ni börjar så godkänner jag den.

                        Ni får använda tekniken från kursen (ASP.NET Core, Vue och en relationsdatabas), men
                        du får byta klientramverk om gruppen är överens. Ett bibliotek för komponenter eller
                        styling får användas men ska inte vara det som tar tid.

                        Applikationen ska uppfylla följande:

                        - API
                          - minst tre resurser med komplett CRUD, där minst en har en relation till en annan
                          - validering och felhantering enligt ProblemDetails
                          - inloggning med JWT och minst två roller med olika rättigheter
                        - Klient
                          - minst fyra vyer, med inloggning och utloggning
                          - laddning, fel och tomma listor hanteras i alla vyer
                          - tillgänglig för tangentbord och skärmläsare
                        - Arbetssätt
                          - allt arbete sker via pull requests med minst en granskare
                          - en pipeline som bygger och kör tester vid varje pull request
                          - en README som beskriver hur man startar systemet och hur ni delat upp arbetet

                        Redovisningen sker i klassrummet på sista dagen, tio minuter per grupp. Visa systemet
                        live och berätta om en sak som ni gjort annorlunda än planerat. ==Alla i gruppen ska kunna
                        förklara någon del av koden==, inte bara sin egen.

                        💡 Tips

                        Gör en tunn första version tidigt, en inloggning som visar en lista, och driftsätt den. Då
                        upptäcker ni problem med miljöer och konfiguration medan det fortfarande är billigt.

                        Håll koll på vem som gör vad. Det ska synas i commit-historiken och i pull requests, och
                        det är det vi utgår från när vi bedömer individuellt.

                        ## Att lämna in

                        - [ ] Länk till repot med README och pipeline
                        - [ ] Svar på vem som gjort vad, med länkar till minst en pull request per person
                        - [ ] En kort reflektion över samarbetet: vad gick bra, vad skulle ni göra annorlunda

                        **Betygskriterier**

                        Godkänt kräver att kraven ovan är uppfyllda och att alla har bidragit. För väl godkänt ska
                        systemet vara driftsatt, ha tester som skyddar det viktigaste och en redovisning som visar
                        att gruppen gjort medvetna val. Laborationen kopplar till kursmål 4: att arbeta i team för
                        att utveckla en webbapplikation.
                        """,
                        Friday(start, 7),
                        GradeScale.PassFailDistinction,
                        _text,
                        SubmissionPattern.Finished,
                        [
                            "Vi byggde en bokningstjänst för gemensamma verktyg i ett bostadsområde. API:et har "
                            + "tre resurser och två roller, och klienten fyra vyer. Alla ändringar gick via pull "
                            + "requests, vi har 14 stycken, varav jag skrev fem och granskade sex. Vi driftsatte "
                            + "på Render.",
                            "Vår applikation är en receptsamling med betyg. Jag ansvarade för inloggning och roller "
                            + "och Emil för klienten. Det vi gjorde annorlunda än planerat var att vi tog bort "
                            + "kommentarsfunktionen för att hinna testa ordentligt. Pipelinen kör både backend- och "
                            + "klienttester.",
                            "Gruppens ärendehanterare för en förening har tre resurser och en pipeline i GitHub "
                            + "Actions. Vi har delat upp arbetet i skivor, och jag gjorde hela flödet för att "
                            + "skapa och tilldela ärenden. README förklarar hur man startar allt med "
                            + "`docker compose up`."
                        ],
                        [
                            "Godkänd. Bra sammanhållen applikation, och det syns i pull requests att alla bidragit. "
                            + "Pipelinen saknar klienttester, men det är inget krav för G.",
                            "Väl godkänd. Ni har driftsatt, testat det som är viktigt och redovisningen om vad ni "
                            + "ändrade på vägen var ärlig och reflekterad.",
                            "Godkänd. Alla kraven är uppfyllda. Din del av pull requests är tydlig, men jag hade "
                            + "gärna sett fler granskningskommentarer i gruppen."
                        ],
                        [
                            "Det saknas en roll med egna rättigheter, och README förklarar inte hur man startar "
                            + "klienten. Lägg till det och lämna in igen.",
                            "Pull requests visar att nästan allt är gjort av en person. Komplettera med vad de "
                            + "andra bidragit med, eller gör en liten ändring var via pull request.",
                            "Valideringen i API:et är ofullständig och klienten visar inga fel vid `400`. Rätta "
                            + "det och beskriv ändringen i svaret."
                        ],
                        "Webbapplikation i grupp")
                ],
                "Teamprojekt")
        ],
        [
            new(
                "Välkommen till Webbapplikationer",
                """
                Nu startar kursen **Webbapplikationer i C#**. Vi bygger ett API med ASP.NET Core under de fyra
                första veckorna, en klient i Vue under de följande två och avslutar med ett teamprojekt.

                Läs veckans teori innan första lektionen och skapa ett repo för laborationerna. Alla uppgifter
                och deadlines hittar ni här i kursen.
                """,
                Se(start.AddDays(-3), 15, 0),
                author,
                true,
                true,
                [
                    new("#2", "Behöver vi ha Vue installerat redan nu, eller kommer det senare?", 5),
                    new(
                        author,
                        "Det räcker med .NET SDK första veckorna. Node installerar vi tillsammans i vecka 5.",
                        7),
                    new("#8", "Kan man använda Visual Studio Code i stället för Rider?", 20),
                    new(author, "Ja, det går bra. Installera C# Dev Kit så får du felsökning och tester.", 22)
                ]),
            new(
                "Tips: CORS-felet som alla får",
                """
                Det här fick jag frågan om tre gånger i dag, så här är svaret till alla.

                När klienten (`localhost:5173`) anropar API:et (`localhost:5000`) blockerar webbläsaren svaret
                eftersom adresserna har olika ursprung. Felet står i konsolen i webbläsaren, inte i API:ets logg,
                och det är därför man inte hittar det på serversidan.

                Det finns två sätt att lösa det. Antingen låter ni Vite proxya anropen (`server.proxy` i
                `vite.config.ts`), så att webbläsaren tror att allt kommer från samma adress. Eller så aktiverar ni
                CORS i API:et för just klientens adress:

                ```csharp
                builder.Services.AddCors(o => o.AddPolicy("client", p => p
                    .WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod()));
                app.UseCors("client");
                ```

                Använd aldrig `AllowAnyOrigin` i produktion.
                """,
                Se(start.AddDays(30), 13, 20),
                author,
                false,
                true,
                [
                    new("#4", "Tusen tack! Jag har suttit med det här sedan i går kväll.", 1),
                    new(
                        "#11",
                        "Jag fick det inte att fungera förrän jag la `app.UseCors` före `UseAuthorization`. Ordningen "
                        + "spelar roll.",
                        3),
                    new(
                        author,
                        "Bra upptäckt, #11. Mellanvaror körs i den ordning de läggs till, så det är en bra sak att "
                        + "veta.",
                        4),
                    new("#4", "Är proxy bättre än CORS?", 5),
                    new(
                        author,
                        "Ingen är bättre rakt av. Proxy är enklast under utveckling, medan CORS behövs om klienten "
                        + "och API:et ligger på olika adresser i produktion.",
                        6),
                    new("#7", "Den som skrev 'AllowAnyOrigin' på fredagens redovisning vet vem den är.", 9)
                ]),
            new(
                "Påminnelse: bokningsuppgiften lämnas in fredag vecka 4",
                """
                En påminnelse: **API-laborationen ska in senast fredag vecka 4 kl. 23.59**. Det som oftast saknas är
                tester för dubbelbokning, och att signeringsnyckeln ligger kvar i repot. Kontrollera båda innan ni
                lämnar in.

                Fastnar ni kan ni boka handledning på tisdag eller torsdag eftermiddag.
                """,
                Se(start.AddDays(22), 9, 10),
                author,
                false,
                true,
                [
                    new("#6", "Måste man ha en riktig databas, eller räcker det med in-memory?", 2),
                    new(
                        author,
                        "In-memory går bra under utveckling, men bokningarna ska finnas kvar efter en omstart. SQLite "
                        + "är det enklaste sättet.",
                        4),
                    new("#13", "Räknas det som dubbelbokning om en bokning slutar 10:00 och nästa börjar 10:00?", 6),
                    new(author, "Nej, det är tillåtet. Bra fråga, skriv gärna ett test för det.", 7)
                ]),
            new(
                "Så gick teamprojekten",
                """
                Tack för två bra redovisningsdagar! Det var roligt att se så många olika idéer, från en
                bokningstjänst för tvättstugan till en app för att hålla koll på fikaschema. Jag hoppas att ni
                tog med er något av det ni såg hos de andra grupperna.

                Bedömningarna är på väg ut. Ni som ska komplettera får en kommentar med vad som saknas, och ni har
                en vecka på er. Hör av er om något är oklart.
                """,
                Se(start.AddDays(51), 14, 30),
                author,
                false,
                true,
                [
                    new("#3", "Tack för all feedback! Vi hade inte kommit på att lägga in kvittot som en egen vy.", 3),
                    new(
                        "#9",
                        "Kan man få se de andra gruppernas repon? Jag vill gärna titta på hur de löste rollerna.",
                        8),
                    new(
                        author,
                        "Fråga gruppen först, men jag tror att de flesta gärna visar. Lägg gärna ut länken här i "
                        + "kommentarerna.",
                        10)
                ])
        ]);
}
