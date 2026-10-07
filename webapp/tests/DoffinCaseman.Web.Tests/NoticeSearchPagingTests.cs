using DoffinCaseman.Web.Services;
using Npgsql;
using Xunit;

namespace DoffinCaseman.Web.Tests;

[Collection("Postgres")]
public class NoticeSearchPagingTests(PostgresFixture fixture)
{
    // The database is shared across tests, so each test seeds notices carrying
    // a unique tag in the title and filters on it via SearchText.
    private async Task<string> SeedNoticesAsync(int count, bool samePublishedDate = false)
    {
        var tag = $"tag{Guid.NewGuid():N}";
        var baseDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        for (var i = 0; i < count; i++)
        {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO notices (notice_id, title, published_date, raw_payload)
                VALUES (@notice_id, @title, @published, '{}')
                """, conn);
            cmd.Parameters.AddWithValue("notice_id", $"{tag}-{i:D3}");
            cmd.Parameters.AddWithValue("title", $"{tag} notice {i}");
            cmd.Parameters.AddWithValue("published", samePublishedDate ? baseDate : baseDate.AddDays(i));
            await cmd.ExecuteNonQueryAsync();
        }
        return tag;
    }

    private NoticeService CreateService() => new(fixture.DbContextFactory);

    [Fact]
    public async Task SearchAsync_ReturnsRequestedPageAndTotals()
    {
        var tag = await SeedNoticesAsync(25);

        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: tag), page: 2, pageSize: 10);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_LastPageHoldsRemainder()
    {
        var tag = await SeedNoticesAsync(25);

        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: tag), page: 3, pageSize: 10);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(3, result.Page);
    }

    [Fact]
    public async Task SearchAsync_OrdersByPublishedDateDescendingAcrossPages()
    {
        var tag = await SeedNoticesAsync(6);
        var service = CreateService();
        var filter = new NoticeFilter(SearchText: tag);

        var first = await service.SearchAsync(filter, page: 1, pageSize: 3);
        var second = await service.SearchAsync(filter, page: 2, pageSize: 3);

        Assert.Equal(
            [$"{tag}-005", $"{tag}-004", $"{tag}-003", $"{tag}-002", $"{tag}-001", $"{tag}-000"],
            first.Items.Concat(second.Items).Select(n => n.NoticeId));
    }

    [Fact]
    public async Task SearchAsync_PagesDoNotOverlapWhenPublishedDatesTie()
    {
        var tag = await SeedNoticesAsync(9, samePublishedDate: true);
        var service = CreateService();
        var filter = new NoticeFilter(SearchText: tag);

        var ids = new List<string>();
        for (var page = 1; page <= 3; page++)
            ids.AddRange((await service.SearchAsync(filter, page, pageSize: 3)).Items.Select(n => n.NoticeId));

        Assert.Equal(9, ids.Distinct().Count());
    }

    [Fact]
    public async Task SearchAsync_PageBeyondLastClampsToLastPage()
    {
        var tag = await SeedNoticesAsync(5);

        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: tag), page: 99, pageSize: 2);

        Assert.Equal(3, result.Page);
        Assert.Single(result.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task SearchAsync_NonPositivePageTreatedAsFirstPage(int page)
    {
        var tag = await SeedNoticesAsync(3);

        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: tag), page, pageSize: 2);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Items.Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(10_000, 200)]
    public async Task SearchAsync_ClampsPageSize(int requested, int expected)
    {
        var tag = await SeedNoticesAsync(3);

        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: tag), pageSize: requested);

        Assert.Equal(expected, result.PageSize);
    }

    [Fact]
    public async Task SearchAsync_NoMatchesReturnsEmptySinglePage()
    {
        var result = await CreateService().SearchAsync(new NoticeFilter(SearchText: $"nomatch{Guid.NewGuid():N}"));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(1, result.Page);
    }
}
