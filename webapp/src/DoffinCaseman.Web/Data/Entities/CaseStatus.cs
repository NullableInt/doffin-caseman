namespace DoffinCaseman.Web.Data.Entities;

// Mirrors the Postgres enum "case_status" defined in
// db/migrations/0004_cases.up.sql. Values are mapped to/from their lowercase
// SQL labels via AppDbContext's HasConversion<string>() with a value
// converter that lowercases, since C# enum member names must be PascalCase.
public enum CaseStatus
{
    New,
    UnderReview,
    Bidding,
    Submitted,
    Won,
    Lost,
    Archived,
}
