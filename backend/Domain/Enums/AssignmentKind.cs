namespace Backend.Domain.Enums;

// Vilken sorts uppgift det är, enligt "Typ av del" i formuläret. Gäller bara content av typen Assignment.
public enum AssignmentKind
{
    Exercise, // övning, ingen betygsättning
    Lab, // laboration, en betygsatt del
    Exam // inlämning som examinerar
}
