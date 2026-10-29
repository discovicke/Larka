namespace Backend.Domain.Entities;

// En engångsändring av data som redan finns i databasen (t.ex. DemoContentRefresh) och som har körts klart.
// Id är namnet på ändringen inklusive version. Finns raden körs ändringen inte igen.
public class DataPatch
{
    public string Id { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
}
