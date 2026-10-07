namespace DoffinCaseman.Web.Data.Entities;

// Maps to "case_assignees": the join between a case and each user working it.
public class CaseAssignee
{
    public long CaseId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    public Case Case { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
}
