using DoffinCaseman.Web.Components;
using DoffinCaseman.Web.Data;
using DoffinCaseman.Web.Data.Entities;
using DoffinCaseman.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' not configured.");

// case_status must be mapped as a native Postgres enum here (not a plain
// connection string passed to UseNpgsql) -- otherwise Npgsql sends
// CaseStatus values as text and Postgres rejects the implicit cast. See
// CaseStatus.cs for why.
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.MapEnum<CaseStatus>("case_status");
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

// Cookie-based auth only -- no external logins, no self-service registration
// (this is an internal staff tool; users are seeded directly, see
// db/seed/dev_seed.sql and docs/doffin-api-notes.md for prod seeding notes).
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 10;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/login";
});

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme);
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<NoticeService>();
builder.Services.AddScoped<CaseService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/login");
}).AllowAnonymous();

app.Run();
