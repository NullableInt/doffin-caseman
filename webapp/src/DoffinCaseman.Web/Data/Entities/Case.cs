namespace DoffinCaseman.Web.Data.Entities;

// Maps to the "cases" table. A case is created lazily the first time staff
// click "Handle" on a notice (see CaseService.GetOrCreateForNoticeAsync).
public class Case
{
    public long Id { get; set; }
    public string NoticeId { get; set; } = default!;
    public CaseStatus Status { get; set; } = CaseStatus.New;
    public Guid? AssigneeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Notice Notice { get; set; } = default!;
    public ApplicationUser? Assignee { get; set; }
    public List<CaseComment> Comments { get; set; } = [];
    public List<CaseStatusHistory> StatusHistory { get; set; } = [];
}
