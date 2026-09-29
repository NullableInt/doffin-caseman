using DoffinCaseman.Web.Data.Entities;
using DoffinCaseman.Web.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DoffinCaseman.Web.Tests;

[Collection("Postgres")]
public class CaseWorkflowTests(PostgresFixture fixture)
{
    private async Task<string> SeedNoticeAsync(string noticeId = "")
    {
        noticeId = string.IsNullOrEmpty(noticeId) ? $"2024-{Guid.NewGuid():N}"[..12] : noticeId;

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO notices (notice_id, title, raw_payload)
            VALUES (@notice_id, 'Test notice', '{}')
            """, conn);
        cmd.Parameters.AddWithValue("notice_id", noticeId);
        await cmd.ExecuteNonQueryAsync();
        return noticeId;
    }

    private async Task<Guid> SeedUserAsync(string userName)
    {
        await using var db = await fixture.DbContextFactory.CreateDbContextAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = userName,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task GetOrCreateForNoticeAsync_CreatesCaseInNewStatus()
    {
        var noticeId = await SeedNoticeAsync();
        var service = new CaseService(fixture.DbContextFactory);

        var @case = await service.GetOrCreateForNoticeAsync(noticeId);

        Assert.Equal(CaseStatus.New, @case.Status);
        Assert.Equal(noticeId, @case.NoticeId);
    }

    [Fact]
    public async Task GetOrCreateForNoticeAsync_IsIdempotent()
    {
        var noticeId = await SeedNoticeAsync();
        var service = new CaseService(fixture.DbContextFactory);

        var first = await service.GetOrCreateForNoticeAsync(noticeId);
        var second = await service.GetOrCreateForNoticeAsync(noticeId);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task ChangeStatusAsync_UpdatesStatusAndWritesHistory()
    {
        var noticeId = await SeedNoticeAsync();
        var userId = await SeedUserAsync("alice-" + Guid.NewGuid().ToString("N")[..8]);
        var service = new CaseService(fixture.DbContextFactory);
        var @case = await service.GetOrCreateForNoticeAsync(noticeId);

        await service.ChangeStatusAsync(@case.Id, CaseStatus.UnderReview, userId);
        await service.ChangeStatusAsync(@case.Id, CaseStatus.Bidding, userId);

        var reloaded = await service.GetByIdAsync(@case.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(CaseStatus.Bidding, reloaded!.Status);

        var history = reloaded.StatusHistory.OrderBy(h => h.ChangedAt).ToList();
        Assert.Equal(2, history.Count);
        Assert.Equal(CaseStatus.New, history[0].OldStatus);
        Assert.Equal(CaseStatus.UnderReview, history[0].NewStatus);
        Assert.Equal(CaseStatus.UnderReview, history[1].OldStatus);
        Assert.Equal(CaseStatus.Bidding, history[1].NewStatus);
        Assert.All(history, h => Assert.Equal(userId, h.ChangedBy));
    }

    [Fact]
    public async Task AddCommentAsync_PersistsCommentWithAttribution()
    {
        var noticeId = await SeedNoticeAsync();
        var userId = await SeedUserAsync("bob-" + Guid.NewGuid().ToString("N")[..8]);
        var service = new CaseService(fixture.DbContextFactory);
        var @case = await service.GetOrCreateForNoticeAsync(noticeId);

        await service.AddCommentAsync(@case.Id, userId, "Looks promising, let's bid.");

        var reloaded = await service.GetByIdAsync(@case.Id);
        Assert.NotNull(reloaded);
        var comment = Assert.Single(reloaded!.Comments);
        Assert.Equal(userId, comment.UserId);
        Assert.Equal("Looks promising, let's bid.", comment.Body);
    }

    [Fact]
    public async Task AddCommentAsync_RejectsEmptyBody()
    {
        var noticeId = await SeedNoticeAsync();
        var userId = await SeedUserAsync("carol-" + Guid.NewGuid().ToString("N")[..8]);
        var service = new CaseService(fixture.DbContextFactory);
        var @case = await service.GetOrCreateForNoticeAsync(noticeId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AddCommentAsync(@case.Id, userId, "   "));
    }
}
