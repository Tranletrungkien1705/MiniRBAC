using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniRBAC.Data;
using MiniRBAC.Models;
using MiniRBAC.Services;
using Serilog;

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
FleetObs.ConfigureLogger("minirbac");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

var conn = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=minirbac.db";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (DbUtil.IsPostgres(conn)) o.UseNpgsql(DbUtil.ToNpgsql(conn));
    else o.UseSqlite(conn);
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<IRbacService, RbacService>();

var ssoAuthority = Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.Authority = ssoAuthority;
    o.RequireHttpsMetadata = ssoAuthority.StartsWith("https");
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = ssoAuthority,
        ValidateAudience = false, ValidateLifetime = true, NameClaimType = "name", RoleClaimType = "role"
    };
});
builder.Services.AddAuthorization();
builder.Services.AddFleetObs();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await Seeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

app.UseFleetObs();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/whoami", (ClaimsPrincipal u) => Results.Ok(new
{
    app = "minirbac",
    sub = u.FindFirst("sub")?.Value, name = u.Identity?.Name ?? u.FindFirst("name")?.Value,
    email = u.FindFirst("email")?.Value, tenant = u.FindFirst("tenant")?.Value,
    roles = u.FindAll("role").Select(c => c.Value)
})).RequireAuthorization();

app.Use(async (ctx, next) =>
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) ctx.Request.Cookies.TryGetValue(TenantContext.CookieName, out key);
    if (!string.IsNullOrWhiteSpace(key))
    {
        using var lookup = app.Services.CreateScope();
        var ldb = lookup.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await ldb.Orgs.FirstOrDefaultAsync(o => o.ApiKey == key);
        if (org != null) ctx.RequestServices.GetRequiredService<ITenantContext>().OrgId = org.Id;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/healthz", () => "ok");

// ===== RBAC (Sys_Access): role / permission / grant / assign / check =====
app.MapPost("/api/roles", async (CodeNameDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.Code) ? Results.BadRequest(new { error = "Cần Code." }) : Results.Ok(await svc.AddRoleAsync(d.Code, d.Name))).RequireAuthorization();
app.MapGet("/api/roles", async (IRbacService svc) => Results.Ok(await svc.ListRolesAsync())).RequireAuthorization();

app.MapPost("/api/permissions", async (CodeNameDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.Code) ? Results.BadRequest(new { error = "Cần Code." }) : Results.Ok(await svc.AddPermissionAsync(d.Code, d.Name, d.Module))).RequireAuthorization();
app.MapGet("/api/permissions", async (IRbacService svc) => Results.Ok(await svc.ListPermissionsAsync())).RequireAuthorization();

app.MapPost("/api/roles/{roleCode}/grant", async (string roleCode, GrantDto d, IRbacService svc) =>
{
    var r = await svc.GrantAsync(roleCode, d.PermissionCode);
    return r is null ? Results.NotFound(new { error = "Role hoặc permission không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();
app.MapPost("/api/roles/{roleCode}/revoke", async (string roleCode, GrantDto d, IRbacService svc) =>
{
    var r = await svc.RevokeAsync(roleCode, d.PermissionCode);
    return r is null ? Results.NotFound(new { error = "Chưa grant." }) : Results.Ok(r);
}).RequireAuthorization();

app.MapPost("/api/users/{userKey}/assign", async (string userKey, AssignDto d, IRbacService svc) =>
{
    var r = await svc.AssignAsync(userKey, d.RoleCode);
    return r is null ? Results.NotFound(new { error = "Role không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();
app.MapPost("/api/users/{userKey}/unassign", async (string userKey, AssignDto d, IRbacService svc) =>
{
    var r = await svc.UnassignAsync(userKey, d.RoleCode);
    return r is null ? Results.NotFound(new { error = "Chưa gán." }) : Results.Ok(r);
}).RequireAuthorization();

app.MapGet("/api/users/{userKey}/permissions", async (string userKey, IRbacService svc) =>
    Results.Ok(await svc.EffectiveAsync(userKey))).RequireAuthorization();

// Check quyền — các app fleet gọi để phân quyền tập trung.
app.MapGet("/api/check", async (string user, string permission, IRbacService svc) =>
    Results.Ok(await svc.CheckAsync(user, permission))).RequireAuthorization();

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "rbc_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

app.Run();

record RegisterOrgDto(string Name);
