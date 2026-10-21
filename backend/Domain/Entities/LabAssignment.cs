namespace Backend.Domain.Entities;

public class LabAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; } // kursen äger labben (en kurs har många labbar)
    public Guid GroupId { get; set; } // klassen labben delats ut till (en kurs läses av många grupper)
    public string Title { get; set; } = "";
    public string TemplateRepo { get; set; } = ""; // "org/template"
    public string UpstreamUrl { get; set; } = "";
}
