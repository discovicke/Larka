namespace Backend.Domain.Enums;

public enum ContentType
{
    News, // kort info, skickar notis
    Theory, // material som bara läses
    Assignment // har deadline och kan kräva inlämning (se AssignmentSpec)
}
