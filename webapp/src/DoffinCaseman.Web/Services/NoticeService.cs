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

// Read-only: the webapp never writes to "notices" (the Go crawler owns that
// table entirely). See AppDbContext remarks.
public class NoticeService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<List<Notice>> SearchAsync(NoticeFilter filter, int page = 1, int pageSize = 25)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Notices.AsNoTracking().Include(n => n.Case).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.CpvCode))
            query = query.Where(n => n.CpvCodes.Contains(filter.CpvCode));

        if (!string.IsNullOrWhiteSpace(filter.Region))
            query = query.Where(n => n.Region == filter.Region);

        if (filter.CaseStatus is not null)
            query = query.Where(n => n.Case != null && n.Case.Status == filter.CaseStatus);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(n => EF.Functions.ILike(n.Title, $"%{filter.SearchText}%"));

        if (filter.PublishedAfter is not null)
            query = query.Where(n => n.PublishedDate >= filter.PublishedAfter);

        if (filter.DeadlineBefore is not null)
            query = query.Where(n => n.Deadline <= filter.DeadlineBefore);

        return await query
            .OrderByDescending(n => n.PublishedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Notice?> GetByNoticeIdAsync(string noticeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Notices.AsNoTracking().FirstOrDefaultAsync(n => n.NoticeId == noticeId);
    }
}
