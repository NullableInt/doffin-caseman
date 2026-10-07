using DoffinCaseman.Web.Data;
using DoffinCaseman.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DoffinCaseman.Web.Services;

public record NoticeFilter(
    string? CpvCode = null,
    string? Region = null,
    CaseStatus? CaseStatus = null,
    string? SearchText = null,
    DateTimeOffset? PublishedAfter = null,
    DateTimeOffset? DeadlineBefore = null);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

// Read-only: the webapp never writes to "notices" (the Go crawler owns that
// table entirely). See AppDbContext remarks.
public class NoticeService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<PagedResult<Notice>> SearchAsync(NoticeFilter filter, int page = 1, int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Notices.AsNoTracking().Include(n => n.Case).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.CpvCode))
            query = query.Where(n => n.CpvCodes.Contains(filter.CpvCode));

        if (!string.IsNullOrWhiteSpace(filter.Region))
            query = query.Where(n => n.RegionCodes.Contains(filter.Region));

        if (filter.CaseStatus is not null)
            query = query.Where(n => n.Case != null && n.Case.Status == filter.CaseStatus);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(n => EF.Functions.ILike(n.Title, $"%{filter.SearchText}%"));

        if (filter.PublishedAfter is not null)
            query = query.Where(n => n.PublishedDate >= filter.PublishedAfter);

        if (filter.DeadlineBefore is not null)
            query = query.Where(n => n.Deadline <= filter.DeadlineBefore);

        var total = await query.CountAsync();

        // Clamp to the last page so a shrinking result set never lands on an empty page.
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, lastPage);

        var items = await query
            .OrderByDescending(n => n.PublishedDate)
            .ThenBy(n => n.NoticeId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Notice>(items, page, pageSize, total);
    }

    public async Task<Notice?> GetByNoticeIdAsync(string noticeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Notices.AsNoTracking().FirstOrDefaultAsync(n => n.NoticeId == noticeId);
    }
}
