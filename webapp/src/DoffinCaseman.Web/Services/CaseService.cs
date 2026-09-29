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
            .Include(c => c.Assignee)
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
            .Include(c => c.Assignee)
            .Include(c => c.Comments.OrderBy(cm => cm.CreatedAt)).ThenInclude(c => c.User)
            .Include(c => c.StatusHistory.OrderBy(h => h.ChangedAt)).ThenInclude(h => h.ChangedByUser)
            .FirstOrDefaultAsync(c => c.Id == caseId);
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

    public async Task AssignAsync(long caseId, Guid? assigneeId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var @case = await db.Cases.FirstOrDefaultAsync(c => c.Id == caseId)
            ?? throw new InvalidOperationException($"Case {caseId} not found.");

        @case.AssigneeId = assigneeId;
        @case.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
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
