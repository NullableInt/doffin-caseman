namespace DoffinCaseman.Web.Data.Entities;

// Maps to "case_comments". ParentCommentId is reserved for future threaded
// replies; v1 always leaves it null and renders a flat chronological list.
public class CaseComment
{
    public long Id { get; set; }
    public long CaseId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public long? ParentCommentId { get; set; }

    public Case Case { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
}
