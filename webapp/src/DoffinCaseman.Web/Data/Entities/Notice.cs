namespace DoffinCaseman.Web.Data.Entities;

// Maps to the "notices" table, owned by db/migrations (see 0002_notices.up.sql).
// Written only by the Go crawler; the webapp reads this table but never
// writes to it directly.
public class Notice
{
    public long Id { get; set; }
    public string NoticeId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? BuyerName { get; set; }
    public string? Description { get; set; }
    public string? NoticeType { get; set; }
    public string? Status { get; set; }
    public string[] CpvCodes { get; set; } = [];
    public string[] RegionCodes { get; set; } = [];
    public DateTimeOffset? PublishedDate { get; set; }
    public DateTimeOffset? Deadline { get; set; }
    public decimal? ContractValueNok { get; set; }
    public string RawPayload { get; set; } = "{}";
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset LastChangedAt { get; set; }

    public Case? Case { get; set; }
}
