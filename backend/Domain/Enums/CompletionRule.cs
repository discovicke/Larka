namespace Backend.Domain.Enums;

// När en uppgift med inlämningssätt räknas som klar. Teori och uppgifter utan inlämningssätt
// klarmarkeras alltid av eleven själv (CompletionRule används inte för dem).
public enum CompletionRule
{
    Submitted, // det räcker att eleven lämnat in
    Approved // eleven måste få G eller VG innan nästa del låses upp
}
