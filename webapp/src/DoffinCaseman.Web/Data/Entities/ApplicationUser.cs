using Microsoft.AspNetCore.Identity;

namespace DoffinCaseman.Web.Data.Entities;

// Backs the "asp_net_users" table (db/migrations/0003_identity.up.sql),
// which is hand-written SQL rather than an EF Core Identity migration -- see
// AppDbContext for the column mapping. DisplayName is a custom addition used
// in the assignee picker and comment bylines.
public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = default!;
}
