using DoffinCaseman.Web.Data;
using DoffinCaseman.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DoffinCaseman.Web.Services;

// The webapp is the sole writer of cases/case_status_history/case_comments.
// ChangeStatusAsync is the one place that updates a case's status, so it's
// the natural place to keep the status write and its history row atomic.
public class CaseService(IDbContextFactory<AppDbContext> dbFactory)
{
    public async Task<Case> GetOrCreateForNoticeAsync(string noticeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var existing = await db.Cases
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .Include(c => c.Comments).ThenInclude(c => c.User)
            .Include(c => c.StatusHistory)
            .FirstOrDefaultAsync(c => c.NoticeId == noticeId);
        if (existing is not null)
            return existing;

        var now = DateTimeOffset.UtcNow;
        var created = new Case
        {
            NoticeId = noticeId,
            Status = CaseStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Cases.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    public async Task<Case?> GetByIdAsync(long caseId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Cases
            .Include(c => c.Notice)
            .Include(c => c.Assignees.OrderBy(a => a.AssignedAt)).ThenInclude(a => a.User)
            .Include(c => c.Comments.OrderBy(cm => cm.CreatedAt)).ThenInclude(c => c.User)
            .Include(c => c.StatusHistory.OrderBy(h => h.ChangedAt)).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(c => c.Id == caseId);
    }

    public async Task<PagedResult<Case>> GetAssignedToAsync(
        Guid assigneeId, bool includeArchived = false, int page = 1, int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        await using var db = await dbFactory.CreateDbContextAsync();

        var query = db.Cases.AsNoTracking().Where(c => c.Assignees.Any(a => a.UserId == assigneeId));
        if (!includeArchived)
            query = query.Where(c => c.Status != CaseStatus.Archived);

        var total = await query.CountAsync();

        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, lastPage);

        var items = await query
            .Include(c => c.Notice)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Case>(items, page, pageSize, total);
    }

    public async Task ChangeStatusAsync(long caseId, CaseStatus newStatus, Guid changedByUserId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var @case = await db.Cases.FirstOrDefaultAsync(c => c.Id == caseId)
            ?? throw new InvalidOperationException($"Case {caseId} not found.");

        var oldStatus = @case.Status;
        @case.Status = newStatus;
        @case.UpdatedAt = DateTimeOffset.UtcNow;

        db.CaseStatusHistories.Add(new CaseStatusHistory
        {
            CaseId = caseId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = changedByUserId,
            ChangedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    // Add/remove are idempotent and touch one assignee at a time, so two
    // people editing the same case's assignees can't overwrite each other.
    public async Task AddAssigneeAsync(long caseId, Guid userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var @case = await db.Cases.FirstOrDefaultAsync(c => c.Id == caseId)
            ?? throw new InvalidOperationException($"Case {caseId} not found.");

        var now = DateTimeOffset.UtcNow;
        // ON CONFLICT keeps a concurrent double-add from failing on the PK.
        var added = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO case_assignees (case_id, user_id, assigned_at) VALUES ({caseId}, {userId}, {now}) ON CONFLICT DO NOTHING");

        if (added > 0)
        {
            @case.UpdatedAt = now;
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }

    public async Task RemoveAssigneeAsync(long caseId, Guid userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var removed = await db.CaseAssignees
            .Where(a => a.CaseId == caseId && a.UserId == userId)
            .ExecuteDeleteAsync();

        if (removed > 0)
        {
            await db.Cases.Where(c => c.Id == caseId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow));
        }
        await tx.CommitAsync();
    }

    public async Task AddCommentAsync(long caseId, Guid userId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Comment body cannot be empty.", nameof(body));

        await using var db = await dbFactory.CreateDbContextAsync();
        db.CaseComments.Add(new CaseComment
        {
            CaseId = caseId,
            UserId = userId,
            Body = body.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<ApplicationUser>> GetAssignableUsersAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();
    }
}
