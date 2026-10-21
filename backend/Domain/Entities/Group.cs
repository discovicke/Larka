using Backend.Domain.Enums;

namespace Backend.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public GroupType Type { get; set; } = GroupType.Class;
    public string Name { get; set; } = "";
    public string Term { get; set; } = "";
}
