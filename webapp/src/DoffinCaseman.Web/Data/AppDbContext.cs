using DoffinCaseman.Web.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DoffinCaseman.Web.Data;

// Schema lifecycle belongs entirely to db/migrations (plain SQL, applied by
// golang-migrate) -- this context is never migrated (no Database.Migrate()
// or EnsureCreated() call anywhere) and only maps onto tables that already
// exist by the time the webapp starts. See docs/doffin-api-notes.md and the
// plan's "Migration ownership" section for the rationale.
//
// All six IdentityDbContext entity sets are mapped, including
// claims/logins/tokens -- confirmed live that even plain username/password
// sign-in needs asp_net_user_claims/asp_net_role_claims to exist (see
// db/migrations/0003_identity.up.sql for why an earlier attempt to skip
// them failed).
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseStatusHistory> CaseStatusHistories => Set<CaseStatusHistory>();
    public DbSet<CaseComment> CaseComments => Set<CaseComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("asp_net_users");
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.UserName).HasColumnName("user_name");
            e.Property(u => u.NormalizedUserName).HasColumnName("normalized_user_name");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.NormalizedEmail).HasColumnName("normalized_email");
            e.Property(u => u.EmailConfirmed).HasColumnName("email_confirmed");
            e.Property(u => u.PasswordHash).HasColumnName("password_hash");
            e.Property(u => u.SecurityStamp).HasColumnName("security_stamp");
            e.Property(u => u.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            e.Property(u => u.PhoneNumber).HasColumnName("phone_number");
            e.Property(u => u.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
            e.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled");
            e.Property(u => u.LockoutEnd).HasColumnName("lockout_end");
            e.Property(u => u.LockoutEnabled).HasColumnName("lockout_enabled");
            e.Property(u => u.AccessFailedCount).HasColumnName("access_failed_count");
            e.Property(u => u.DisplayName).HasColumnName("display_name");
        });

        modelBuilder.Entity<IdentityRole<Guid>>(e =>
        {
            e.ToTable("asp_net_roles");
            e.Property(r => r.Id).HasColumnName("id");
            e.Property(r => r.Name).HasColumnName("name");
            e.Property(r => r.NormalizedName).HasColumnName("normalized_name");
            e.Property(r => r.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        });

        modelBuilder.Entity<IdentityUserRole<Guid>>(e =>
        {
            e.ToTable("asp_net_user_roles");
            e.Property(r => r.UserId).HasColumnName("user_id");
            e.Property(r => r.RoleId).HasColumnName("role_id");
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(e =>
        {
            e.ToTable("asp_net_user_claims");
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.ClaimType).HasColumnName("claim_type");
            e.Property(c => c.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(e =>
        {
            e.ToTable("asp_net_role_claims");
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.RoleId).HasColumnName("role_id");
            e.Property(c => c.ClaimType).HasColumnName("claim_type");
            e.Property(c => c.ClaimValue).HasColumnName("claim_value");
        });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(e =>
        {
            e.ToTable("asp_net_user_logins");
            e.Property(l => l.LoginProvider).HasColumnName("login_provider");
            e.Property(l => l.ProviderKey).HasColumnName("provider_key");
            e.Property(l => l.ProviderDisplayName).HasColumnName("provider_display_name");
            e.Property(l => l.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<IdentityUserToken<Guid>>(e =>
        {
            e.ToTable("asp_net_user_tokens");
            e.Property(t => t.UserId).HasColumnName("user_id");
            e.Property(t => t.LoginProvider).HasColumnName("login_provider");
            e.Property(t => t.Name).HasColumnName("name");
            e.Property(t => t.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Notice>(e =>
        {
            e.ToTable("notices");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasColumnName("id");
            e.Property(n => n.NoticeId).HasColumnName("notice_id");
            e.Property(n => n.Title).HasColumnName("title");
            e.Property(n => n.BuyerName).HasColumnName("buyer_name");
            e.Property(n => n.Description).HasColumnName("description");
            e.Property(n => n.NoticeType).HasColumnName("notice_type");
            e.Property(n => n.Status).HasColumnName("status");
            e.Property(n => n.CpvCodes).HasColumnName("cpv_codes").HasColumnType("text[]");
            e.Property(n => n.RegionCodes).HasColumnName("region_codes").HasColumnType("text[]");
            e.Property(n => n.PublishedDate).HasColumnName("published_date");
            e.Property(n => n.Deadline).HasColumnName("deadline");
            e.Property(n => n.ContractValueNok).HasColumnName("contract_value_nok");
            e.Property(n => n.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
            e.Property(n => n.FirstSeenAt).HasColumnName("first_seen_at");
            e.Property(n => n.LastSeenAt).HasColumnName("last_seen_at");
            e.Property(n => n.LastChangedAt).HasColumnName("last_changed_at");
            e.HasIndex(n => n.NoticeId).IsUnique();

            // The webapp only ever reads notices; the crawler is the sole writer.
            // (No EF migrations exist for this project at all -- see class remarks.)
        });

        modelBuilder.Entity<Case>(e =>
        {
            e.ToTable("cases");
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.NoticeId).HasColumnName("notice_id");
            // Mapped as a native Postgres enum via
            // NpgsqlDataSourceBuilder.MapEnum<CaseStatus>() in Program.cs /
            // PostgresFixture.cs -- no HasConversion needed (or wanted: an
            // earlier string-conversion attempt failed live against real
            // Postgres, see CaseStatus.cs).
            e.Property(c => c.Status)
                .HasColumnName("status")
                .HasColumnType("case_status");
            e.Property(c => c.AssigneeId).HasColumnName("assignee_id");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(c => c.Notice)
                .WithOne(n => n.Case)
                .HasForeignKey<Case>(c => c.NoticeId)
                .HasPrincipalKey<Notice>(n => n.NoticeId);

            e.HasOne(c => c.Assignee)
                .WithMany()
                .HasForeignKey(c => c.AssigneeId);

            e.HasIndex(c => c.NoticeId).IsUnique();
        });

        modelBuilder.Entity<CaseStatusHistory>(e =>
        {
            e.ToTable("case_status_history");
            e.Property(h => h.Id).HasColumnName("id");
            e.Property(h => h.CaseId).HasColumnName("case_id");
            e.Property(h => h.OldStatus)
                .HasColumnName("old_status")
                .HasColumnType("case_status");
            e.Property(h => h.NewStatus)
                .HasColumnName("new_status")
                .HasColumnType("case_status");
            e.Property(h => h.ChangedBy).HasColumnName("changed_by");
            e.Property(h => h.ChangedAt).HasColumnName("changed_at");

            e.HasOne(h => h.Case)
                .WithMany(c => c.StatusHistory)
                .HasForeignKey(h => h.CaseId);

            e.HasOne(h => h.ChangedByUser)
                .WithMany()
                .HasForeignKey(h => h.ChangedBy);
        });

        modelBuilder.Entity<CaseComment>(e =>
        {
            e.ToTable("case_comments");
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.CaseId).HasColumnName("case_id");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.Body).HasColumnName("body");
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
            e.Property(c => c.ParentCommentId).HasColumnName("parent_comment_id");

            e.HasOne(c => c.Case)
                .WithMany(c => c.Comments)
                .HasForeignKey(c => c.CaseId);

            e.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId);
        });
    }
}
