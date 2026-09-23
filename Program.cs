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
FleetObs.ReportLicense(ssoAuthority, "minirbac");
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

// ===== Sys_Object catalog + Sys_Access_CheckDeny (nguồn 2010.HTC) =====
// Danh mục đối tượng/chức năng: ObjectType (MENU/SCR/BTN/WS/WSFUNC/APP), cây theo ObjectCodeParent, FlagActive.
app.MapPost("/api/objects", async (SysObjectDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.ObjectCode) ? Results.BadRequest(new { error = "Cần ObjectCode." }) : Results.Ok(await svc.AddObjectAsync(d))).RequireAuthorization();
app.MapGet("/api/objects", async (string? type, bool? activeOnly, IRbacService svc) =>
    Results.Ok(await svc.ListObjectsAsync(type, activeOnly))).RequireAuthorization();
app.MapGet("/api/objects/tree", async (IRbacService svc) => Results.Ok(await svc.ObjectTreeAsync())).RequireAuthorization();

// Cờ quản trị hệ thống (Sys_User.FlagSysAdmin) — bypass trong deny-check.
app.MapPost("/api/users/{userKey}/sysadmin", async (string userKey, bool flag, IRbacService svc) =>
    Results.Ok(await svc.SetSysAdminAsync(userKey, flag))).RequireAuthorization();

// Deny-check (Sys_Access_CheckDeny): user được phép object khi thuộc role có grant object ĐANG HOẠT ĐỘNG,
// hoặc có FlagSysAdmin. Object không hoạt động => luôn chặn.
app.MapGet("/api/checkdeny", async (string user, string objectCode, IRbacService svc) =>
    Results.Ok(await svc.CheckDenyAsync(user, objectCode))).RequireAuthorization();

// ===== SysObjectSetting + ResolveObjects (nguồn 2010.HTC) =====
// Cấu hình FUNC gắn với object (SysObjectSetting.config): gán 1 object (vd AUTH_SYSGROUP) sẽ bao hàm
// các mã chức năng con (Sys_Group_Create, Sys_Group_Delete, ...). Dùng khi Sys_Access_Save.
app.MapPut("/api/objects/{objectCode}/functions", async (string objectCode, ObjectFunctionsDto d, IRbacService svc) =>
    Results.Ok(await svc.SetObjectFunctionsAsync(objectCode, d.FunctionCodes))).RequireAuthorization();
app.MapGet("/api/objects/functions", async (string? objectCode, IRbacService svc) =>
    Results.Ok(await svc.ListObjectFunctionsAsync(objectCode))).RequireAuthorization();

// ResolveObjects: mở rộng danh sách object thành tập mã FUNC (giữ object gốc + các FUNC cấu hình).
app.MapPost("/api/objects/resolve", async (ResolveObjectsDto d, IRbacService svc) =>
    Results.Ok(await svc.ResolveObjectsAsync(d.ObjectCodes))).RequireAuthorization();

// Import hàng loạt Role thật từ Sys_Group + gán UserRole thật cho user có FlagSysAdmin/FlagSysViewer
// (SQL nguồn 2010.HTC). Sys_Function ở nguồn RỖNG (0 dòng) nên KHÔNG import Permission/RolePermission —
// không suy diễn dữ liệu không có thật.
app.MapPost("/api/import/roles", async (List<CodeNameDto> rows, IRbacService svc) =>
{
    if (rows is null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu import." });
    int n = 0;
    foreach (var r in rows)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)) continue;
        await svc.AddRoleAsync(r.Code, r.Name); n++;
    }
    return Results.Ok(new { imported = n, total = rows.Count });
}).RequireAuthorization();

app.MapPost("/api/import/userroles", async (List<ImportUserRoleDto> rows, IRbacService svc) =>
{
    if (rows is null || rows.Count == 0) return Results.BadRequest(new { error = "Không có dữ liệu import." });
    int n = 0;
    foreach (var r in rows)
    {
        if (string.IsNullOrWhiteSpace(r.UserCode)) continue;
        if (r.FlagSysAdmin == "1") { await svc.AddRoleAsync("SYSADMIN", "Quản trị hệ thống (Sys_Access)"); await svc.AssignAsync(r.UserCode, "SYSADMIN"); n++; }
        if (r.FlagSysViewer == "1") { await svc.AddRoleAsync("SYSVIEWER", "Xem hệ thống (Sys_Access)"); await svc.AssignAsync(r.UserCode, "SYSVIEWER"); n++; }
    }
    return Results.Ok(new { assigned = n, total = rows.Count });
}).RequireAuthorization();

// ===== Sys_UserTeam + phạm vi dữ liệu (nguồn 2010.HTC) =====
// Đội bán hàng: khóa (TeamCode, DealerCode), FlagActive.
app.MapPost("/api/teams", async (UserTeamDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.TeamCode) || string.IsNullOrWhiteSpace(d.DealerCode)
        ? Results.BadRequest(new { error = "Cần TeamCode và DealerCode." })
        : Results.Ok(await svc.AddTeamAsync(d))).RequireAuthorization();
app.MapGet("/api/teams", async (string? dealerCode, bool? activeOnly, IRbacService svc) =>
    Results.Ok(await svc.ListTeamsAsync(dealerCode, activeOnly))).RequireAuthorization();

// Thành viên đội (Sys_UserInTeam_Save): thay TOÀN BỘ thành viên của đội trong 1 thao tác.
// Ràng buộc nguồn: mỗi user chỉ thuộc MỘT đội duy nhất (one-user-one-team).
app.MapPut("/api/teams/{teamCode}/{dealerCode}/members", async (string teamCode, string dealerCode, GroupMembersDto d, IRbacService svc) =>
{
    var r = await svc.SetTeamMembersAsync(teamCode, dealerCode, d.UserCodes);
    if (r is null) return Results.NotFound(new { error = "Đội không tồn tại." });
    return Results.Ok(r);
}).RequireAuthorization();
app.MapGet("/api/teams/{teamCode}/{dealerCode}/members", async (string teamCode, string dealerCode, IRbacService svc) =>
{
    var r = await svc.ListTeamMembersAsync(teamCode, dealerCode);
    return r is null ? Results.NotFound(new { error = "Đội không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();

// Phạm vi dữ liệu user (Sys_User): cờ vai trò + vị trí.
app.MapPost("/api/users/{userKey}/scope", async (string userKey, UserScopeDto d, IRbacService svc) =>
    Results.Ok(await svc.SetUserScopeAsync(d with { UserKey = userKey }))).RequireAuthorization();

// View/Write ability (Sys_UserTeam): tập user được xem/ghi theo phạm vi đội.
app.MapGet("/api/users/{userKey}/viewability", async (string userKey, IRbacService svc) =>
    Results.Ok(await svc.ViewAbilityAsync(userKey))).RequireAuthorization();

// ===== Sys_Group + Sys_UserInGroup (nguồn 2010.HTC) =====
// Nhóm quyền: khóa GroupCode, FlagActive (chỉ nhóm hoạt động mới tính khi check quyền).
app.MapPost("/api/groups", async (GroupDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.GroupCode) ? Results.BadRequest(new { error = "Cần GroupCode." }) : Results.Ok(await svc.AddGroupAsync(d))).RequireAuthorization();
app.MapGet("/api/groups", async (bool? activeOnly, IRbacService svc) =>
    Results.Ok(await svc.ListGroupsAsync(activeOnly))).RequireAuthorization();

// Sys_Group_Create (nguồn 2010.HTC): TẠO nhóm mới — GroupCode bắt buộc + CHƯA tồn tại, GroupName bắt buộc.
// Khác POST /api/groups (AddGroupAsync upsert không kiểm tra trùng).
app.MapPost("/api/groups/create", async (GroupDto d, IRbacService svc) =>
    Results.Ok(await svc.CreateGroupAsync(d))).RequireAuthorization();

// Sys_Group_Update (nguồn 2010.HTC): cập nhật nhóm (partial theo Ft_Cols_Upd) — nhóm phải tồn tại,
// GroupName bắt buộc khi cập nhật.
app.MapPut("/api/groups/{groupCode}", async (string groupCode, UpdateGroupDto d, IRbacService svc) =>
    Results.Ok(await svc.UpdateGroupAsync(groupCode, d))).RequireAuthorization();

// Sys_Group_Delete (nguồn 2010.HTC): xóa nhóm + dọn mọi thành viên (Sys_UserInGroup) của nhóm.
app.MapDelete("/api/groups/{groupCode}", async (string groupCode, IRbacService svc) =>
{
    var r = await svc.DeleteGroupAsync(groupCode);
    return r.Ok ? Results.Ok(r) : Results.NotFound(r);
}).RequireAuthorization();

// Thành viên nhóm (Sys_UserInGroup_Save): thay TOÀN BỘ thành viên của nhóm trong 1 thao tác.
app.MapPut("/api/groups/{groupCode}/members", async (string groupCode, GroupMembersDto d, IRbacService svc) =>
{
    var r = await svc.SetGroupMembersAsync(groupCode, d.UserCodes);
    return r is null ? Results.NotFound(new { error = "Nhóm không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();
app.MapGet("/api/groups/{groupCode}/members", async (string groupCode, IRbacService svc) =>
{
    var r = await svc.ListGroupMembersAsync(groupCode);
    return r is null ? Results.NotFound(new { error = "Nhóm không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();

// Các nhóm mà user thuộc về (Sys_UserInGroup).
app.MapGet("/api/users/{userKey}/groups", async (string userKey, IRbacService svc) =>
    Results.Ok(await svc.GroupsOfUserAsync(userKey))).RequireAuthorization();

// ===== Sys_Access (nguồn 2010.HTC) =====
// Grant object cho nhóm (Sys_Access_Save): thay TOÀN BỘ grant của nhóm trong 1 thao tác
// (xóa hết grant cũ rồi ghi danh sách object mới). Chỉ nhận object đang hoạt động.
app.MapPut("/api/groups/{groupCode}/access", async (string groupCode, GroupAccessDto d, IRbacService svc) =>
{
    var r = await svc.SetGroupAccessAsync(groupCode, d.ObjectCodes);
    return r is null ? Results.NotFound(new { error = "Nhóm không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();
app.MapGet("/api/groups/{groupCode}/access", async (string groupCode, IRbacService svc) =>
{
    var r = await svc.ListGroupAccessAsync(groupCode);
    return r is null ? Results.NotFound(new { error = "Nhóm không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();

// GetAccess (nguồn 2010.HTC, SysAccessController.GetAccess): màn "Thêm chức năng cho nhóm" —
// trả về TOÀN BỘ danh mục object kèm cờ granted (đã grant cho nhóm hay chưa) để tick chọn.
// Khác GET /api/groups/{code}/access (chỉ liệt kê object ĐÃ grant).
app.MapGet("/api/groups/{groupCode}/access-screen", async (string groupCode, string? type, bool? activeOnly, IRbacService svc) =>
{
    var r = await svc.GetGroupAccessScreenAsync(groupCode, type, activeOnly);
    return r is null ? Results.NotFound(new { error = "Nhóm không tồn tại." }) : Results.Ok(r);
}).RequireAuthorization();

// ===== Sys_User_GetForCurrentUser (nguồn 2010.HTC) =====
// Hồ sơ user hiện tại (Sys_User + đội qua Sys_UserInTeam→Sys_UserTeam) và danh sách object HIỆU LỰC:
// hợp của object đang hoạt động được grant qua các nhóm đang hoạt động mà user thuộc về,
// và (nếu FlagSysAdmin='1') tất cả object đang hoạt động. Dùng để dựng menu/chức năng cho user.
app.MapGet("/api/users/{userKey}/current", async (string userKey, IRbacService svc) =>
    Results.Ok(await svc.GetForCurrentUserAsync(userKey))).RequireAuthorization();

// ===== Sys_User_GetByViewAbility (nguồn 2010.HTC) =====
// Tập user được XEM (read) / GHI (write) của user hiện tại, theo ViewAbilityType (ADMIN/ALL/TEAM/USER)
// và DealerCode gốc ('HTC') hay đại lý con. Dùng để lọc danh sách user theo phạm vi nhìn dữ liệu.
app.MapGet("/api/users/{userKey}/viewability2", async (string userKey, IRbacService svc) =>
    Results.Ok(await svc.GetByViewAbilityAsync(userKey))).RequireAuthorization();

// myCache_ViewAbility_CheckAccessUser: user hiện tại có quyền GHI lên targetUserCode không.
app.MapGet("/api/users/{userKey}/canaccess/{targetUserCode}", async (string userKey, string targetUserCode, IRbacService svc) =>
    Results.Ok(await svc.CheckAccessUserAsync(userKey, targetUserCode))).RequireAuthorization();

// ===== Sys_User_Login + Sys_User_ChangePassword (nguồn 2010.HTC) =====
// Đăng nhập: user tồn tại + đang hoạt động, đại lý của user tồn tại + đang hoạt động, mật khẩu khớp.
app.MapPost("/api/login", async (LoginDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.UserCode) ? Results.BadRequest(new { error = "Cần UserCode." })
        : Results.Ok(await svc.LoginAsync(d.UserCode, d.Password ?? "")));

// Đổi mật khẩu: user tồn tại + đang hoạt động, mật khẩu cũ khớp, ghi mật khẩu mới.
app.MapPost("/api/users/{userKey}/changepassword", async (string userKey, ChangePasswordDto d, IRbacService svc) =>
    Results.Ok(await svc.ChangePasswordAsync(userKey, d.OldPassword ?? "", d.NewPassword ?? ""))).RequireAuthorization();

// ===== Sys_User_Create (nguồn 2010.HTC) =====
// Tạo hồ sơ user mới kèm kiểm tra ràng buộc: UserCode bắt buộc + chưa tồn tại; DealerCode phải tồn tại;
// DeptCode phải tồn tại + đang hoạt động theo đại lý; UserStaffId (nếu có) duy nhất theo đại lý;
// ViewAbilityType/UserName/UserPassword bắt buộc. Ghi với FlagActive='1'.
app.MapPost("/api/users", async (CreateUserDto d, IRbacService svc) =>
    string.IsNullOrWhiteSpace(d.UserCode) ? Results.BadRequest(new { error = "Cần UserCode." })
        : Results.Ok(await svc.CreateUserAsync(d))).RequireAuthorization();

// ===== Sys_User_Update (nguồn 2010.HTC) =====
// Cập nhật hồ sơ user (partial theo Cols = Ft_Cols_Upd; rỗng = tất cả cột cho phép) kèm kiểm tra ràng buộc:
// user phải tồn tại; ViewAbilityType/UserName bắt buộc khi cập nhật; UserStaffId (nếu cập nhật, khác rỗng)
// phải duy nhất theo đại lý. DealerCode/DeptCode KHÔNG cập nhật (giống nguồn).
app.MapPut("/api/users/{userKey}", async (string userKey, UpdateUserDto d, IRbacService svc) =>
    Results.Ok(await svc.UpdateUserAsync(userKey, d))).RequireAuthorization();

// ===== Sys_User_Delete (nguồn 2010.HTC) =====
// Xóa hồ sơ user kèm dọn liên kết: xóa mọi dòng Sys_UserInGroup và Sys_UserInTeam của user,
// rồi xóa dòng Sys_User. User phải tồn tại (Sys_User_CheckDB, Flag.Yes).
app.MapDelete("/api/users/{userKey}", async (string userKey, IRbacService svc) =>
{
    var r = await svc.DeleteUserAsync(userKey);
    return r.Ok ? Results.Ok(r) : Results.NotFound(r);
}).RequireAuthorization();

app.MapPost("/api/orgs/register", async (RegisterOrgDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name)) return Results.BadRequest(new { error = "Cần Name." });
    var org = new Org { Name = dto.Name.Trim(), ApiKey = "rbc_" + Guid.NewGuid().ToString("N") };
    db.Orgs.Add(org); await db.SaveChangesAsync();
    return Results.Ok(new { orgId = org.Id, apiKey = org.ApiKey });
});

app.Run();

record RegisterOrgDto(string Name);
record ObjectFunctionsDto(List<string> FunctionCodes);
record ResolveObjectsDto(List<string> ObjectCodes);
record ImportUserRoleDto(string? UserCode, string? FlagSysAdmin, string? FlagSysViewer);
record GroupAccessDto(List<string> ObjectCodes);
record LoginDto(string UserCode, string? Password);
record ChangePasswordDto(string? OldPassword, string? NewPassword);
