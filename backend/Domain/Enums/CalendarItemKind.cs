namespace Backend.Domain.Enums;

public enum CalendarItemKind
{
    Deadline, // uppgiftens inlämningstid
    Publish, // när en del blir synlig för eleverna
    ResubmitDeadline, // en kompletteringsdeadline
    PersonalDeadline // elevens egen extra deadline
}
