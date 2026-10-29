using System.Text;

namespace Backend.Infrastructure.Persistence;

/// <summary>En person i demodatan. Nyckeln används för att peka ut läraren som skrivit något.</summary>
internal sealed record DemoPerson(string Key, string Name)
{
    public string Email => DemoPeople.EmailOf(Name);
}

/// <summary>
/// Namn och slumpval till demodatan. Allt är deterministiskt (fasta listor och en egen hash i stället för Random),
/// så att två körningar, och testerna, ger exakt samma data.
/// </summary>
internal static class DemoPeople
{
    public const string EmailDomain = "demo.larka.se";

    public static readonly DemoPerson[] Teachers =
    [
        new("karin", "Karin Holmgren"),
        new("anders", "Anders Wikström"),
        new("jenny", "Jenny Åberg"),
        new("patrik", "Patrik Sundqvist"),
        new("mikael", "Mikael Strand"),
        new("camilla", "Camilla Dahlberg"),
        new("roger", "Roger Hedström")
    ];

    public static readonly DemoPerson[] Mentors =
    [
        new("malin", "Malin Ekström"),
        new("henrik", "Henrik Lindahl"),
        new("sofie", "Sofie Brännström")
    ];

    private static readonly string[] _firstNames =
    [
        "Emma", "Erik", "Sofia", "Lucas", "Elin", "Oscar", "Maja", "Hugo", "Linnea", "Filip", "Alva", "Viktor",
        "Wilma", "Axel", "Tilda", "Emil", "Hanna", "Anton", "Matilda", "Jonas", "Ida", "Mohammed", "Amanda", "Albin",
        "Julia", "Simon", "Nora", "William", "Selma", "Noah", "Agnes", "Isak", "Johanna", "Gustav", "Sara", "Elias",
        "Frida", "Marcus", "Leyla", "Kevin", "Ebba", "David", "Nadia", "Johan", "Klara", "Samuel", "Mariam", "Adam",
        "Cecilia", "Jakob", "Rebecka", "Dragan", "Ellen", "Nils", "Astrid", "Rasmus", "Thea", "Ali"
    ];

    private static readonly string[] _lastNames =
    [
        "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson", "Olsson", "Persson", "Svensson",
        "Gustafsson", "Pettersson", "Jonsson", "Jansson", "Hansson", "Bengtsson", "Lindberg", "Jakobsson",
        "Magnusson", "Lindström", "Olofsson", "Lindqvist", "Lindgren", "Axelsson", "Berg", "Bergström", "Lundberg",
        "Lind", "Lundgren", "Berglund", "Fredriksson", "Sandberg", "Henriksson", "Forsberg", "Sjöberg", "Wallin",
        "Engström", "Eklund", "Danielsson", "Håkansson", "Lundin", "Holm", "Nyström", "Isaksson", "Strömberg",
        "Öberg", "Hedlund", "Åkesson", "Khalil", "Hassan", "Yilmaz", "Novak", "Petrović", "Ibrahim", "Rahimi",
        "Mattsson", "Sundström", "Ahmadi"
    ];

    /// <summary>
    /// Elev nummer index. Förnamn och efternamn stegas med olika steg som saknar gemensam delare med listornas
    /// längd, så att ingen kombination återkommer inom 60 elever.
    /// </summary>
    public static DemoPerson Student(int index) => new(
        $"elev{index}",
        $"{_firstNames[(index * 7 + 3) % _firstNames.Length]} {_lastNames[(index * 11 + 5) % _lastNames.Length]}");

    /// <summary>fornamn.efternamn@demo.larka.se, utan å, ä, ö och andra accenttecken.</summary>
    public static string EmailOf(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var local = string.Join('.', parts.Select(Fold)).ToLowerInvariant();
        return $"{local}@{EmailDomain}";
    }

    private static string Fold(string text)
    {
        var builder = new StringBuilder();
        foreach (var letter in text)
        {
            builder.Append(letter switch
            {
                'å' or 'ä' or 'Å' or 'Ä' => 'a',
                'ö' or 'Ö' => 'o',
                'é' or 'É' => 'e',
                'ć' or 'Ć' => 'c',
                _ => letter
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Ett deterministiskt "slumptal" 0–99 av några heltal (FNV-hash). Samma indata ger alltid samma tal, till
    /// skillnad från HashCode och Random, så att demodatan blir densamma varje gång.
    /// </summary>
    public static int Roll(params int[] seed)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var value in seed)
            {
                hash = (hash ^ (uint)value) * 16777619u;
            }

            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (int)(hash % 100u);
        }
    }

    /// <summary>Svensk tid. Sommartid gäller från sista söndagen i mars till sista söndagen i oktober.</summary>
    public static DateTimeOffset Se(int year, int month, int day, int hour = 0, int minute = 0)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        var summerStart = LastSunday(year, 3);
        var summerEnd = LastSunday(year, 10);
        var isSummer = local.Date >= summerStart && local.Date < summerEnd;
        var offset = TimeSpan.FromHours(isSummer
            ? 2
            : 1);

        // Npgsql tar bara emot UTC (offset 0) för timestamptz.
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static DateTimeOffset Se(DateOnly date, int hour = 0, int minute = 0) =>
        Se(date.Year, date.Month, date.Day, hour, minute);

    private static DateTime LastSunday(int year, int month)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-(int)last.DayOfWeek);
    }
}
