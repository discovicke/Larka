namespace Backend.Domain.Enums;

public enum GroupType
{
    Class, // t.ex. NET25 – läser kurser via GroupCourse
    Committee, // t.ex. studentrepresentantsgrupp – fristående, inga kurser
    Other
}
