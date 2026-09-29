namespace DoffinCaseman.Web.Data.Entities;

// Maps to "case_status_history" -- one append-only row per status change,
// written exclusively by CaseService.ChangeStatusAsync.
public class CaseStatusHistory
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public CaseStatus? OldStatus { get; set; }
    public CaseStatus NewStatus { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    public Case Case { get; set; } = default!;
    public ApplicationUser? ChangedByUser { get; set; }
}
