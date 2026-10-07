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

    [Fact]
    public async Task GetAssignedToAsync_ReturnsOnlyCasesAssignedToUser()
    {
        var me = await SeedUserAsync($"me-{Guid.NewGuid():N}");
        var other = await SeedUserAsync($"other-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);

        var mine = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        var theirs = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync()); // unassigned
        await service.AddAssigneeAsync(mine.Id, me);
        await service.AddAssigneeAsync(theirs.Id, other);

        var result = await service.GetAssignedToAsync(me);

        Assert.Equal([mine.Id], result.Items.Select(c => c.Id));
        Assert.Equal(1, result.TotalCount);
        Assert.NotNull(result.Items[0].Notice);
    }

    [Fact]
    public async Task GetAssignedToAsync_HidesArchivedUnlessRequested()
    {
        var me = await SeedUserAsync($"me-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);

        var active = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        var archived = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        await service.AddAssigneeAsync(active.Id, me);
        await service.AddAssigneeAsync(archived.Id, me);
        await service.ChangeStatusAsync(archived.Id, CaseStatus.Archived, me);

        var hidden = await service.GetAssignedToAsync(me);
        var shown = await service.GetAssignedToAsync(me, includeArchived: true);

        Assert.Equal([active.Id], hidden.Items.Select(c => c.Id));
        Assert.Equal(2, shown.TotalCount);
    }

    [Fact]
    public async Task GetAssignedToAsync_PagesNewestUpdatedFirst()
    {
        var me = await SeedUserAsync($"me-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);

        var ids = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var c = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
            await service.AddAssigneeAsync(c.Id, me); // each assign bumps UpdatedAt
            ids.Add(c.Id);
        }

        var first = await service.GetAssignedToAsync(me, page: 1, pageSize: 2);
        var second = await service.GetAssignedToAsync(me, page: 2, pageSize: 2);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal([ids[2], ids[1], ids[0]], first.Items.Concat(second.Items).Select(c => c.Id));
    }

    [Fact]
    public async Task AddAssigneeAsync_SupportsMultipleAssigneesAndIsIdempotent()
    {
        var alice = await SeedUserAsync($"alice-{Guid.NewGuid():N}");
        var bob = await SeedUserAsync($"bob-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);
        var @case = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());

        await service.AddAssigneeAsync(@case.Id, alice);
        await service.AddAssigneeAsync(@case.Id, bob);
        await service.AddAssigneeAsync(@case.Id, alice); // duplicate: no error, no second row

        var loaded = await service.GetByIdAsync(@case.Id);
        Assert.NotNull(loaded);
        Assert.Equal(
            new[] { alice, bob }.Order(),
            loaded.Assignees.Select(a => a.UserId).Order());
    }

    [Fact]
    public async Task RemoveAssigneeAsync_RemovesOnlyThatUser()
    {
        var alice = await SeedUserAsync($"alice-{Guid.NewGuid():N}");
        var bob = await SeedUserAsync($"bob-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);
        var @case = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        await service.AddAssigneeAsync(@case.Id, alice);
        await service.AddAssigneeAsync(@case.Id, bob);

        await service.RemoveAssigneeAsync(@case.Id, alice);
        await service.RemoveAssigneeAsync(@case.Id, alice); // already gone: no error

        var loaded = await service.GetByIdAsync(@case.Id);
        Assert.Equal([bob], loaded!.Assignees.Select(a => a.UserId));
    }

    [Fact]
    public async Task GetAssignedToAsync_ReturnsCaseForEveryAssignee()
    {
        var alice = await SeedUserAsync($"alice-{Guid.NewGuid():N}");
        var bob = await SeedUserAsync($"bob-{Guid.NewGuid():N}");
        var service = new CaseService(fixture.DbContextFactory);
        var shared = await service.GetOrCreateForNoticeAsync(await SeedNoticeAsync());
        await service.AddAssigneeAsync(shared.Id, alice);
        await service.AddAssigneeAsync(shared.Id, bob);

        var forAlice = await service.GetAssignedToAsync(alice);
        var forBob = await service.GetAssignedToAsync(bob);

        Assert.Equal([shared.Id], forAlice.Items.Select(c => c.Id));
        Assert.Equal([shared.Id], forBob.Items.Select(c => c.Id));
        Assert.Equal(1, forAlice.TotalCount); // joined rows must not duplicate the case
    }
}
