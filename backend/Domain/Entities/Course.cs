namespace Backend.Domain.Entities;

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public string Title { get; set; } = "";
    public string Code { get; set; } = "";

    /// <summary>Kursbeskrivning i Markdown, visas på fliken Om.</summary>
    public string Description { get; set; } = "";

    /// <summary>Kursens omfattning i yrkeshögskolepoäng, om den är satt.</summary>
    public int? Points { get; set; }

    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
}
