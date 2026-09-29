using NpgsqlTypes;

namespace DoffinCaseman.Web.Data.Entities;

// Mirrors the Postgres enum "case_status" defined in
// db/migrations/0004_cases.up.sql. Mapped as a native Postgres enum via
// NpgsqlDataSourceBuilder.MapEnum<CaseStatus>("case_status") (see Program.cs
// and PostgresFixture.cs) -- explicit [PgName] on every member rather than
// relying on Npgsql's default snake_case translator, since a silent
// mismatch there would fail at runtime, not compile time. (An earlier
// EF Core HasConversion<string> approach failed live: Npgsql sent the value
// as plain text, and Postgres refused to implicitly cast text to a
// user-defined enum type -- "column is of type case_status but expression
// is of type text".)
public enum CaseStatus
{
    [PgName("new")] New,
    [PgName("under_review")] UnderReview,
    [PgName("bidding")] Bidding,
    [PgName("submitted")] Submitted,
    [PgName("won")] Won,
    [PgName("lost")] Lost,
    [PgName("archived")] Archived,
}
