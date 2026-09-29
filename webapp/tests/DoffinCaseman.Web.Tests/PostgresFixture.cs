using DoffinCaseman.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DoffinCaseman.Web.Tests;

// Spins up a real PostgreSQL instance and applies the repo's db/migrations
// SQL files directly (the same files golang-migrate applies in production),
// so tests exercise the exact schema the webapp runs against rather than an
// EF Core "ensure created" approximation of it.
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("docker.io/library/postgres:16-alpine")
        .WithDatabase("doffin_test")
        .WithUsername("doffin")
        .WithPassword("doffin")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public IDbContextFactory<AppDbContext> DbContextFactory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await ApplyMigrationsAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        DbContextFactory = new PooledDbContextFactory<AppDbContext>(options);
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationsDir = FindMigrationsDir();
        var files = Directory.GetFiles(migrationsDir, "*.up.sql").OrderBy(f => f);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string FindMigrationsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "db", "migrations")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Could not locate db/migrations relative to the test output directory.");

        return Path.Combine(dir.FullName, "db", "migrations");
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
