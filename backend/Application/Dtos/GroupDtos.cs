using Backend.Domain.Entities;
using Backend.Domain.Enums;

namespace Backend.Application.Dtos;

public record GroupDto(Guid Id, Guid SchoolId, GroupType Type, string Name, string Term)
{
    public static GroupDto From(Group group) =>
        new(group.Id, group.SchoolId, group.Type, group.Name, group.Term);
}

public record CourseDto(Guid Id, Guid SchoolId, string Title, string Code)
{
    public static CourseDto From(Course course) =>
        new(course.Id, course.SchoolId, course.Title, course.Code);
}
