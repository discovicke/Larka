namespace Backend.Domain.Entities;

// En projektgrupp inom en utdelning. Gruppens delade repo hänger på en av gruppens
// inlämningar ( StudentRepo.AssignmentGroupId ), så att provisionering, checks och lås
// kan återanvända samma flöde som individuella repon.
public class AssignmentGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentLinkId { get; set; }
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public List<AssignmentGroupMember> Members { get; set; } = [];
    public StudentRepo? Repo { get; set; }
}
