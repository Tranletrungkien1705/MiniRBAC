using Microsoft.EntityFrameworkCore;
using MiniRBAC.Data;
using MiniRBAC.Models;

namespace MiniRBAC.Services;

public record CodeNameDto(string Code, string Name, string? Module);
public record GrantDto(string PermissionCode);
public record AssignDto(string RoleCode);
public record SysObjectDto(string ObjectCode, string ObjectName, string? ObjectType, string? ObjectCodeParent, bool? FlagActive);
public record UserTeamDto(string TeamCode, string DealerCode, string TeamName, bool? FlagActive);
public record UserScopeDto(string UserKey, string? DealerCode, string? DBCode, string? TeamCode, bool? FlagSysAdmin, bool? FlagDBAdmin, bool? FlagTeamLeader, bool? FlagSalesman);
public record GroupDto(string GroupCode, string GroupName, bool? FlagActive);
public record GroupMembersDto(List<string> UserCodes);
public record UpdateGroupDto(string? GroupName, bool? FlagActive, List<string>? Cols);
public record DeleteGroupResult(bool Ok, string Reason, string GroupCode, int RemovedMembers);
public record ViewAbilityDto(string UserCode, string? DealerCode, string? DealerBUPattern, string? ViewAbilityType, bool? FlagSysAdmin, bool? FlagActive);
public record CreateUserDto(string UserCode, string? DealerCode, string? DeptCode, string? UserStaffId, string? UserName, string? UserPassword, string? UserEmail, string? UserPhoneNo, string? ViewAbilityType, bool? FlagSysAdmin);
public record UpdateUserDto(string? UserStaffId, string? UserName, string? UserPassword, string? UserEmail, string? UserPhoneNo, string? ViewAbilityType, bool? FlagSysAdmin, bool? FlagActive, List<string>? Cols);
public record DeleteUserResult(bool Ok, string Reason, string UserCode, int RemovedGroups, int RemovedTeams);

public interface IRbacService
{
    Task<object> AddRoleAsync(string code, string name);
    Task<object> ListRolesAsync();
    Task<object> AddPermissionAsync(string code, string name, string? module);
    Task<object> ListPermissionsAsync();
    Task<object?> GrantAsync(string roleCode, string permCode);
    Task<object?> RevokeAsync(string roleCode, string permCode);
    Task<object?> AssignAsync(string userKey, string roleCode);
    Task<object?> UnassignAsync(string userKey, string roleCode);
    Task<object> EffectiveAsync(string userKey);
    Task<object> CheckAsync(string userKey, string permCode);
    // Sys_Object catalog + Sys_Access_CheckDeny (nguồn 2010.HTC)
    Task<object> AddObjectAsync(SysObjectDto d);
    Task<object> ListObjectsAsync(string? type, bool? activeOnly);
    Task<object> ObjectTreeAsync();
    Task<object> SetSysAdminAsync(string userKey, bool flag);
    Task<object> CheckDenyAsync(string userKey, string objectCode);
    // SysObjectSetting + ResolveObjects (nguồn 2010.HTC): cấu hình FUNC gắn với object
    Task<object> SetObjectFunctionsAsync(string objectCode, List<string> functionCodes);
    Task<object> ListObjectFunctionsAsync(string? objectCode);
    Task<object> ResolveObjectsAsync(List<string> objectCodes);
    // Sys_UserTeam + phạm vi dữ liệu (nguồn 2010.HTC)
    Task<object> AddTeamAsync(UserTeamDto d);
    Task<object> ListTeamsAsync(string? dealerCode, bool? activeOnly);
    Task<object> SetUserScopeAsync(UserScopeDto d);
    Task<object> ViewAbilityAsync(string userKey);
    // Sys_UserInTeam_Save (nguồn 2010.HTC): thay TOÀN BỘ thành viên của đội trong 1 thao tác
    Task<object?> SetTeamMembersAsync(string teamCode, string dealerCode, List<string> userCodes);
    Task<object?> ListTeamMembersAsync(string teamCode, string dealerCode);
    // Sys_Group + Sys_UserInGroup (nguồn 2010.HTC)
    Task<object> AddGroupAsync(GroupDto d);
    Task<object> ListGroupsAsync(bool? activeOnly);
    // Sys_Group_Create / Sys_Group_Update / Sys_Group_Delete (nguồn 2010.HTC)
    Task<object> CreateGroupAsync(GroupDto d);
    Task<object> UpdateGroupAsync(string groupCode, UpdateGroupDto d);
    Task<DeleteGroupResult> DeleteGroupAsync(string groupCode);
    Task<object?> SetGroupMembersAsync(string groupCode, List<string> userCodes);
    Task<object?> ListGroupMembersAsync(string groupCode);
    Task<object> GroupsOfUserAsync(string userCode);
    // Sys_Access (nguồn 2010.HTC): grant object cho nhóm, thay toàn bộ trong 1 lần
    Task<object?> SetGroupAccessAsync(string groupCode, List<string> objectCodes);
    Task<object?> ListGroupAccessAsync(string groupCode);
    // GetAccess (nguồn 2010.HTC): màn gán chức năng cho nhóm — toàn bộ object kèm cờ granted
    Task<object?> GetGroupAccessScreenAsync(string groupCode, string? type, bool? activeOnly);
    // Sys_User_GetForCurrentUser (nguồn 2010.HTC): hồ sơ user hiện tại + danh sách object hiệu lực
    Task<object> GetForCurrentUserAsync(string userKey);
    // Sys_User_GetByViewAbility + myCache_ViewAbility_CheckAccessUser (nguồn 2010.HTC)
    Task<object> GetByViewAbilityAsync(string userKey);
    Task<object> CheckAccessUserAsync(string userKey, string targetUserCode);
    // Sys_User_Login + Sys_User_ChangePassword (nguồn 2010.HTC)
    Task<object> LoginAsync(string userCode, string password);
    Task<object> ChangePasswordAsync(string userCode, string oldPassword, string newPassword);
    // Sys_User_Create (nguồn 2010.HTC): tạo hồ sơ user kèm kiểm tra ràng buộc
    Task<object> CreateUserAsync(CreateUserDto d);
    // Sys_User_Update (nguồn 2010.HTC): cập nhật hồ sơ user (partial theo Ft_Cols_Upd) kèm kiểm tra ràng buộc
    Task<object> UpdateUserAsync(string userCode, UpdateUserDto d);
    // Sys_User_Delete (nguồn 2010.HTC): xóa hồ sơ user + dọn thành viên nhóm/đội của user
    Task<DeleteUserResult> DeleteUserAsync(string userCode);
}

public sealed class RbacService(AppDbContext db, ITenantContext tenant) : IRbacService
{
    private Guid Org => tenant.OrgId;

    public async Task<object> AddRoleAsync(string code, string name)
    {
        code = code.Trim().ToUpperInvariant();
        var r = await db.Roles.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == code);
        if (r is null) { r = new Role { OrgId = Org, Code = code, Name = name.Trim() }; db.Roles.Add(r); }
        else r.Name = name.Trim();
        await db.SaveChangesAsync();
        return new { r.Code, r.Name };
    }

    public async Task<object> ListRolesAsync()
    {
        var items = await db.Roles.Where(x => x.OrgId == Org).OrderBy(x => x.Code).Select(x => new
        {
            x.Code, x.Name,
            permissions = db.RolePermissions.Count(p => p.OrgId == Org && p.RoleCode == x.Code),
            users = db.UserRoles.Count(u => u.OrgId == Org && u.RoleCode == x.Code)
        }).ToListAsync();
        return new { count = items.Count, items };
    }

    public async Task<object> AddPermissionAsync(string code, string name, string? module)
    {
        code = code.Trim().ToUpperInvariant();
        var p = await db.Permissions.FirstOrDefaultAsync(x => x.OrgId == Org && x.Code == code);
        if (p is null) { p = new Permission { OrgId = Org, Code = code, Name = name.Trim(), Module = module }; db.Permissions.Add(p); }
        else { p.Name = name.Trim(); p.Module = module; }
        await db.SaveChangesAsync();
        return new { p.Code, p.Name, p.Module };
    }

    public async Task<object> ListPermissionsAsync()
    {
        var items = await db.Permissions.Where(x => x.OrgId == Org).OrderBy(x => x.Code)
            .Select(x => new { x.Code, x.Name, x.Module }).ToListAsync();
        return new { count = items.Count, items };
    }

    public async Task<object?> GrantAsync(string roleCode, string permCode)
    {
        roleCode = roleCode.Trim().ToUpperInvariant(); permCode = permCode.Trim().ToUpperInvariant();
        if (!await db.Roles.AnyAsync(x => x.OrgId == Org && x.Code == roleCode)) return null;
        if (!await db.Permissions.AnyAsync(x => x.OrgId == Org && x.Code == permCode)) return null;
        if (!await db.RolePermissions.AnyAsync(x => x.OrgId == Org && x.RoleCode == roleCode && x.PermissionCode == permCode))
        {
            db.RolePermissions.Add(new RolePermission { OrgId = Org, RoleCode = roleCode, PermissionCode = permCode });
            await db.SaveChangesAsync();
        }
        return new { roleCode, permissionCode = permCode, granted = true };
    }

    public async Task<object?> RevokeAsync(string roleCode, string permCode)
    {
        roleCode = roleCode.Trim().ToUpperInvariant(); permCode = permCode.Trim().ToUpperInvariant();
        var rp = await db.RolePermissions.FirstOrDefaultAsync(x => x.OrgId == Org && x.RoleCode == roleCode && x.PermissionCode == permCode);
        if (rp is null) return null;
        db.RolePermissions.Remove(rp);
        await db.SaveChangesAsync();
        return new { roleCode, permissionCode = permCode, granted = false };
    }

    public async Task<object?> AssignAsync(string userKey, string roleCode)
    {
        userKey = userKey.Trim(); roleCode = roleCode.Trim().ToUpperInvariant();
        if (!await db.Roles.AnyAsync(x => x.OrgId == Org && x.Code == roleCode)) return null;
        if (!await db.UserRoles.AnyAsync(x => x.OrgId == Org && x.UserKey == userKey && x.RoleCode == roleCode))
        {
            db.UserRoles.Add(new UserRole { OrgId = Org, UserKey = userKey, RoleCode = roleCode });
            await db.SaveChangesAsync();
        }
        return new { userKey, roleCode, assigned = true };
    }

    public async Task<object?> UnassignAsync(string userKey, string roleCode)
    {
        userKey = userKey.Trim(); roleCode = roleCode.Trim().ToUpperInvariant();
        var ur = await db.UserRoles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserKey == userKey && x.RoleCode == roleCode);
        if (ur is null) return null;
        db.UserRoles.Remove(ur);
        await db.SaveChangesAsync();
        return new { userKey, roleCode, assigned = false };
    }

    public async Task<object> EffectiveAsync(string userKey)
    {
        userKey = userKey.Trim();
        var roles = await db.UserRoles.Where(x => x.OrgId == Org && x.UserKey == userKey).Select(x => x.RoleCode).ToListAsync();
        var perms = await db.RolePermissions.Where(x => x.OrgId == Org && roles.Contains(x.RoleCode))
            .Select(x => x.PermissionCode).Distinct().OrderBy(x => x).ToListAsync();
        return new { userKey, roles, permissions = perms };
    }

    // Mô hình ALLOW (Sys_Access): chưa grant = chặn. allowed = user có role nào grant quyền này.
    public async Task<object> CheckAsync(string userKey, string permCode)
    {
        userKey = userKey.Trim(); permCode = permCode.Trim().ToUpperInvariant();
        var roles = await db.UserRoles.Where(x => x.OrgId == Org && x.UserKey == userKey).Select(x => x.RoleCode).ToListAsync();
        var allowed = roles.Count > 0 && await db.RolePermissions.AnyAsync(x => x.OrgId == Org && roles.Contains(x.RoleCode) && x.PermissionCode == permCode);
        return new { userKey, permission = permCode, allowed };
    }

    // ===== Sys_Object catalog (nguồn 2010.HTC) =====
    public async Task<object> AddObjectAsync(SysObjectDto d)
    {
        var code = d.ObjectCode.Trim().ToUpperInvariant();
        var type = string.IsNullOrWhiteSpace(d.ObjectType) ? "MENU" : d.ObjectType.Trim().ToUpperInvariant();
        var parent = string.IsNullOrWhiteSpace(d.ObjectCodeParent) ? null : d.ObjectCodeParent.Trim().ToUpperInvariant();
        var o = await db.SysObjects.FirstOrDefaultAsync(x => x.OrgId == Org && x.ObjectCode == code);
        if (o is null) { o = new SysObject { OrgId = Org, ObjectCode = code }; db.SysObjects.Add(o); }
        o.ObjectName = d.ObjectName.Trim();
        o.ObjectType = type;
        o.ObjectCodeParent = parent;
        o.FlagActive = d.FlagActive ?? true;
        await db.SaveChangesAsync();
        return new { o.ObjectCode, o.ObjectName, o.ObjectType, o.ObjectCodeParent, o.FlagActive };
    }

    public async Task<object> ListObjectsAsync(string? type, bool? activeOnly)
    {
        var q = db.SysObjects.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(type)) { var t = type.Trim().ToUpperInvariant(); q = q.Where(x => x.ObjectType == t); }
        if (activeOnly == true) q = q.Where(x => x.FlagActive);
        var items = await q.OrderBy(x => x.ObjectCode)
            .Select(x => new { x.ObjectCode, x.ObjectName, x.ObjectType, x.ObjectCodeParent, x.FlagActive }).ToListAsync();
        return new { count = items.Count, items };
    }

    // Dựng cây menu/chức năng theo ObjectCodeParent (giống Sys_Object nguồn).
    public async Task<object> ObjectTreeAsync()
    {
        var all = await db.SysObjects.Where(x => x.OrgId == Org)
            .Select(x => new { x.ObjectCode, x.ObjectName, x.ObjectType, x.ObjectCodeParent, x.FlagActive })
            .ToListAsync();
        var byParent = all.GroupBy(x => x.ObjectCodeParent ?? "").ToDictionary(g => g.Key, g => g.OrderBy(x => x.ObjectCode).ToList());
        object Build(string parent) => byParent.TryGetValue(parent, out var kids)
            ? kids.Select(k => new { k.ObjectCode, k.ObjectName, k.ObjectType, k.FlagActive, children = Build(k.ObjectCode) }).ToList()
            : new List<object>();
        return new { roots = Build("") };
    }

    public async Task<object> SetSysAdminAsync(string userKey, bool flag)
    {
        userKey = userKey.Trim();
        var f = await db.SysUserFlags.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserKey == userKey);
        if (f is null) { f = new SysUserFlag { OrgId = Org, UserKey = userKey }; db.SysUserFlags.Add(f); }
        f.FlagSysAdmin = flag;
        await db.SaveChangesAsync();
        return new { userKey, flagSysAdmin = f.FlagSysAdmin };
    }

    // Sys_Access_CheckDeny (nguồn 2010.HTC): user được phép object khi
    //  (a) user thuộc role đang hoạt động có grant object ĐANG HOẠT ĐỘNG, HOẶC
    //  (b) user có FlagSysAdmin='1' (bypass). Object không hoạt động => luôn chặn.
    public async Task<object> CheckDenyAsync(string userKey, string objectCode)
    {
        userKey = userKey.Trim(); objectCode = objectCode.Trim().ToUpperInvariant();
        var obj = await db.SysObjects.FirstOrDefaultAsync(x => x.OrgId == Org && x.ObjectCode == objectCode);
        if (obj is null) return new { userKey, objectCode, allowed = false, reason = "object_not_found" };
        if (!obj.FlagActive) return new { userKey, objectCode, allowed = false, reason = "object_inactive" };
        var isAdmin = await db.SysUserFlags.AnyAsync(x => x.OrgId == Org && x.UserKey == userKey && x.FlagSysAdmin && x.FlagActive);
        if (isAdmin) return new { userKey, objectCode, allowed = true, reason = "sysadmin" };
        var roles = await db.UserRoles.Where(x => x.OrgId == Org && x.UserKey == userKey).Select(x => x.RoleCode).ToListAsync();
        var granted = roles.Count > 0 && await db.RolePermissions.AnyAsync(x => x.OrgId == Org && roles.Contains(x.RoleCode) && x.PermissionCode == objectCode);
        return new { userKey, objectCode, allowed = granted, reason = granted ? "granted" : "denied" };
    }

    // ===== SysObjectSetting + ResolveObjects (nguồn 2010.HTC) =====
    // UpdateSysObjectFunctions: ghi cấu hình FUNC gắn với 1 object (thay toàn bộ danh sách FUNC của object).
    public async Task<object> SetObjectFunctionsAsync(string objectCode, List<string> functionCodes)
    {
        objectCode = (objectCode ?? "").Trim().ToUpperInvariant();
        var codes = (functionCodes ?? new List<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim()).Distinct().ToList();
        var f = await db.SysObjectFunctions.FirstOrDefaultAsync(x => x.OrgId == Org && x.ObjectCode == objectCode);
        if (f is null) { f = new SysObjectFunction { OrgId = Org, ObjectCode = objectCode }; db.SysObjectFunctions.Add(f); }
        f.FunctionCodes = string.Join(",", codes);
        await db.SaveChangesAsync();
        return new { f.ObjectCode, functionCodes = codes };
    }

    // GetSysObjectSetting: đọc cấu hình FUNC (theo 1 object hoặc toàn bộ).
    public async Task<object> ListObjectFunctionsAsync(string? objectCode)
    {
        var q = db.SysObjectFunctions.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(objectCode)) { var oc = objectCode.Trim().ToUpperInvariant(); q = q.Where(x => x.ObjectCode == oc); }
        var items = await q.OrderBy(x => x.ObjectCode)
            .Select(x => new { x.ObjectCode, x.FunctionCodes }).ToListAsync();
        return new { count = items.Count, items };
    }

    // ResolveObjects (nguồn 2010.HTC): mở rộng danh sách object thành tập mã FUNC.
    // Với mỗi object: giữ chính nó, rồi thêm các FUNC trong cấu hình SysObjectSetting của nó
    // (mỗi phần tử FunctionCodes có thể chứa nhiều mã phân tách bằng dấu phẩy). Khử trùng lặp.
    public async Task<object> ResolveObjectsAsync(List<string> objectCodes)
    {
        var input = (objectCodes ?? new List<string>())
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().ToUpperInvariant()).Distinct().ToList();
        var setting = await db.SysObjectFunctions.Where(x => x.OrgId == Org).ToListAsync();
        var byObject = setting.ToDictionary(x => x.ObjectCode, x => x.FunctionCodes, StringComparer.OrdinalIgnoreCase);

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in input)
        {
            if (seen.Add(code)) result.Add(code);
            if (!byObject.TryGetValue(code, out var funcs)) continue;
            foreach (var part in funcs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (seen.Add(part)) result.Add(part);
        }
        return new { input, resolved = result, count = result.Count };
    }

    // ===== Sys_UserTeam (nguồn 2010.HTC) =====
    public async Task<object> AddTeamAsync(UserTeamDto d)
    {
        var teamCode = d.TeamCode.Trim().ToUpperInvariant();
        var dealerCode = d.DealerCode.Trim().ToUpperInvariant();
        var t = await db.SysUserTeams.FirstOrDefaultAsync(x => x.OrgId == Org && x.TeamCode == teamCode && x.DealerCode == dealerCode);
        if (t is null) { t = new SysUserTeam { OrgId = Org, TeamCode = teamCode, DealerCode = dealerCode }; db.SysUserTeams.Add(t); }
        t.TeamName = d.TeamName.Trim();
        t.FlagActive = d.FlagActive ?? true;
        await db.SaveChangesAsync();
        return new { t.TeamCode, t.DealerCode, t.TeamName, t.FlagActive };
    }

    public async Task<object> ListTeamsAsync(string? dealerCode, bool? activeOnly)
    {
        var q = db.SysUserTeams.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(dealerCode)) { var dc = dealerCode.Trim().ToUpperInvariant(); q = q.Where(x => x.DealerCode == dc); }
        if (activeOnly == true) q = q.Where(x => x.FlagActive);
        var items = await q.OrderBy(x => x.DealerCode).ThenBy(x => x.TeamCode)
            .Select(x => new { x.TeamCode, x.DealerCode, x.TeamName, x.FlagActive }).ToListAsync();
        return new { count = items.Count, items };
    }

    // Phạm vi dữ liệu user (Sys_User): cờ vai trò + vị trí (DealerCode/DBCode/TeamCode).
    public async Task<object> SetUserScopeAsync(UserScopeDto d)
    {
        var userKey = d.UserKey.Trim();
        var s = await db.SysUserScopes.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserKey == userKey);
        if (s is null) { s = new SysUserScope { OrgId = Org, UserKey = userKey }; db.SysUserScopes.Add(s); }
        s.DealerCode = (d.DealerCode ?? "").Trim().ToUpperInvariant();
        s.DBCode = (d.DBCode ?? "").Trim().ToUpperInvariant();
        s.TeamCode = (d.TeamCode ?? "").Trim().ToUpperInvariant();
        s.FlagSysAdmin = d.FlagSysAdmin ?? false;
        s.FlagDBAdmin = d.FlagDBAdmin ?? false;
        s.FlagTeamLeader = d.FlagTeamLeader ?? false;
        s.FlagSalesman = d.FlagSalesman ?? false;
        await db.SaveChangesAsync();
        return new { s.UserKey, s.DealerCode, s.DBCode, s.TeamCode, s.FlagSysAdmin, s.FlagDBAdmin, s.FlagTeamLeader, s.FlagSalesman };
    }

    // Sys_UserTeam View/Write ability (nguồn 2010.HTC, myCache_Sys_UserTeam_ViewAbility_Get).
    // Trả về tập UserCode mà user được XEM (read) và được GHI (write) trong phạm vi đội.
    // Thứ tự ưu tiên nhánh giống nguồn: SysAdmin > DBAdmin > TeamLeader > Salesman.
    public async Task<object> ViewAbilityAsync(string userKey)
    {
        userKey = userKey.Trim();
        var me = await db.SysUserScopes.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserKey == userKey && x.FlagActive);
        if (me is null) return new { userKey, read = Array.Empty<string>(), write = Array.Empty<string>(), scope = "none" };

        var all = await db.SysUserScopes.Where(x => x.OrgId == Org && x.FlagActive).ToListAsync();
        List<string> read, write; string scope;

        if (me.FlagSysAdmin)
        {
            // SysAdmin: xem tất cả; ghi trong cùng DealerCode.
            scope = "sysadmin";
            read = all.Select(x => x.UserKey).Distinct().ToList();
            write = all.Where(x => x.DealerCode == me.DealerCode).Select(x => x.UserKey).Distinct().ToList();
        }
        else if (me.FlagDBAdmin)
        {
            // DBAdmin: xem/ghi trong cùng DealerCode.
            scope = "dbadmin";
            read = all.Where(x => x.DealerCode == me.DealerCode).Select(x => x.UserKey).Distinct().ToList();
            write = read.ToList();
        }
        else if (me.FlagTeamLeader && !string.IsNullOrEmpty(me.TeamCode))
        {
            // TeamLeader: xem/ghi các thành viên cùng đội (TeamCode + DealerCode).
            scope = "teamleader";
            read = all.Where(x => x.TeamCode == me.TeamCode && x.DealerCode == me.DealerCode).Select(x => x.UserKey).Distinct().ToList();
            write = read.ToList();
        }
        else if (me.FlagSalesman)
        {
            // Salesman: chỉ chính mình.
            scope = "salesman";
            read = new List<string> { me.UserKey };
            write = new List<string> { me.UserKey };
        }
        else
        {
            scope = "none";
            read = new List<string>();
            write = new List<string>();
        }
        return new { userKey, scope, read, write };
    }

    // ===== Sys_UserInTeam_Save (nguồn 2010.HTC) =====
    // Thay TOÀN BỘ thành viên của đội (TeamCode, DealerCode) trong 1 thao tác:
    // xóa hết thành viên cũ của đội rồi ghi danh sách mới. Trả null nếu đội không tồn tại.
    // Ràng buộc nghiệp vụ nguồn (Sys_UserInTeam_Save_InvalidOneUserOneTeam): mỗi user chỉ thuộc
    // MỘT đội duy nhất — nếu sau khi ghi có user nằm ở >1 đội thì từ chối toàn bộ thao tác.
    public async Task<object?> SetTeamMembersAsync(string teamCode, string dealerCode, List<string> userCodes)
    {
        teamCode = teamCode.Trim().ToUpperInvariant();
        dealerCode = dealerCode.Trim().ToUpperInvariant();
        if (!await db.SysUserTeams.AnyAsync(x => x.OrgId == Org && x.TeamCode == teamCode && x.DealerCode == dealerCode))
            return null;

        var wanted = (userCodes ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim()).Distinct().ToList();

        // Thay toàn bộ: xóa thành viên cũ của đội rồi ghi danh sách mới.
        var old = await db.SysUserInTeams.Where(x => x.OrgId == Org && x.TeamCode == teamCode && x.DealerCode == dealerCode).ToListAsync();
        db.SysUserInTeams.RemoveRange(old);
        foreach (var u in wanted)
            db.SysUserInTeams.Add(new SysUserInTeam { OrgId = Org, UserCode = u, TeamCode = teamCode, DealerCode = dealerCode });

        // Kiểm tra ràng buộc 1-user-1-đội trên trạng thái sau khi ghi (chưa lưu).
        var all = await db.SysUserInTeams.Where(x => x.OrgId == Org).ToListAsync();
        var pending = all.Where(x => !(x.TeamCode == teamCode && x.DealerCode == dealerCode)).ToList();
        pending.AddRange(wanted.Select(u => new SysUserInTeam { OrgId = Org, UserCode = u, TeamCode = teamCode, DealerCode = dealerCode }));
        var dup = pending.GroupBy(x => x.UserCode).FirstOrDefault(g => g.Select(x => x.TeamCode + "|" + x.DealerCode).Distinct().Count() > 1);
        if (dup is not null)
            return new { error = "one_user_one_team", userCode = dup.Key, teams = dup.Select(x => new { x.TeamCode, x.DealerCode }).Distinct().ToList() };

        await db.SaveChangesAsync();
        return new { teamCode, dealerCode, members = wanted };
    }

    // Sys_UserTeam_Get: danh sách thành viên của đội (kèm tên user nếu có hồ sơ Sys_User).
    public async Task<object?> ListTeamMembersAsync(string teamCode, string dealerCode)
    {
        teamCode = teamCode.Trim().ToUpperInvariant();
        dealerCode = dealerCode.Trim().ToUpperInvariant();
        var t = await db.SysUserTeams.FirstOrDefaultAsync(x => x.OrgId == Org && x.TeamCode == teamCode && x.DealerCode == dealerCode);
        if (t is null) return null;
        var members = await db.SysUserInTeams.Where(x => x.OrgId == Org && x.TeamCode == teamCode && x.DealerCode == dealerCode)
            .Select(x => x.UserCode).OrderBy(x => x).ToListAsync();
        return new { t.TeamCode, t.DealerCode, t.TeamName, t.FlagActive, members };
    }

    // ===== Sys_Group + Sys_UserInGroup (nguồn 2010.HTC) =====
    // Nhóm quyền: khóa GroupCode, FlagActive (chỉ nhóm hoạt động mới tính khi check quyền).
    public async Task<object> AddGroupAsync(GroupDto d)
    {
        var code = d.GroupCode.Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == code);
        if (g is null) { g = new SysGroup { OrgId = Org, GroupCode = code }; db.SysGroups.Add(g); }
        g.GroupName = d.GroupName.Trim();
        g.FlagActive = d.FlagActive ?? true;
        await db.SaveChangesAsync();
        return new { g.GroupCode, g.GroupName, g.FlagActive };
    }

    public async Task<object> ListGroupsAsync(bool? activeOnly)
    {
        var q = db.SysGroups.Where(x => x.OrgId == Org);
        if (activeOnly == true) q = q.Where(x => x.FlagActive);
        var items = await q.OrderBy(x => x.GroupCode).Select(x => new
        {
            x.GroupCode, x.GroupName, x.FlagActive,
            members = db.SysUserInGroups.Count(m => m.OrgId == Org && m.GroupCode == x.GroupCode)
        }).ToListAsync();
        return new { count = items.Count, items };
    }

    // ===== Sys_Group_Create (nguồn 2010.HTC) =====
    // Tạo nhóm quyền mới kèm kiểm tra ràng buộc giống nguồn:
    //  (1) GroupCode bắt buộc (Sys_Group_Create_InvalidGroupCode);
    //  (2) GroupCode CHƯA tồn tại (Sys_Group_CheckDB, Flag.No → Sys_Group_CheckDB_GroupCodeExist);
    //  (3) GroupName bắt buộc (Sys_Group_Create_InvalidGroupName). Ghi với FlagActive='1'.
    // Khác AddGroupAsync (upsert không kiểm tra trùng) — đây là thao tác TẠO thuần theo nguồn.
    public async Task<object> CreateGroupAsync(GroupDto d)
    {
        var code = (d.GroupCode ?? "").Trim().ToUpperInvariant();
        var name = (d.GroupName ?? "").Trim();
        if (code.Length == 0) return new { ok = false, reason = "invalid_groupcode" };
        if (await db.SysGroups.AnyAsync(x => x.OrgId == Org && x.GroupCode == code))
            return new { ok = false, reason = "groupcode_exist", groupCode = code };
        if (name.Length == 0) return new { ok = false, reason = "invalid_groupname" };

        var g = new SysGroup { OrgId = Org, GroupCode = code, GroupName = name, FlagActive = true };
        db.SysGroups.Add(g);
        await db.SaveChangesAsync();
        return new { ok = true, reason = "ok", group = new { g.GroupCode, g.GroupName, g.FlagActive } };
    }

    // ===== Sys_Group_Update (nguồn 2010.HTC) =====
    // Cập nhật nhóm quyền (partial theo Ft_Cols_Upd) kèm kiểm tra ràng buộc giống nguồn:
    //  (1) Sys_Group_CheckDB(Flag.Yes): nhóm phải TỒN TẠI, nếu không trả reason group_not_found;
    //  (2) nếu cập nhật GroupName thì phải KHÁC RỖNG (Sys_Group_Update_InvalidGroupName);
    //  (3) FlagActive cập nhật khi có trong Cols. Cols rỗng/null = cập nhật tất cả cột cho phép.
    public async Task<object> UpdateGroupAsync(string groupCode, UpdateGroupDto d)
    {
        groupCode = (groupCode ?? "").Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == groupCode);
        if (g is null) return new { ok = false, reason = "group_not_found", groupCode };

        // Ft_Cols_Upd: danh sách cột cần cập nhật (rỗng = tất cả). So khớp không phân biệt hoa/thường.
        var cols = (d.Cols ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
        bool Upd(string col) => cols.Count == 0 || cols.Contains(col.ToUpperInvariant());

        var name = (d.GroupName ?? "").Trim();
        if (Upd("GroupName") && name.Length == 0)
            return new { ok = false, reason = "invalid_groupname", groupCode };

        if (Upd("GroupName")) g.GroupName = name;
        if (Upd("FlagActive")) g.FlagActive = d.FlagActive ?? true;

        await db.SaveChangesAsync();
        return new { ok = true, reason = "ok", group = new { g.GroupCode, g.GroupName, g.FlagActive } };
    }

    // ===== Sys_Group_Delete (nguồn 2010.HTC) =====
    // Xóa nhóm quyền kèm dọn thành viên giống nguồn:
    //  (1) Sys_Group_CheckDB(Flag.Yes): nhóm phải TỒN TẠI, nếu không trả reason group_not_found;
    //  (2) Sys_UserInGroup_Delete_ByGroup: xóa mọi dòng Sys_UserInGroup của nhóm;
    //  (3) xóa dòng Sys_Group. Toàn bộ trong 1 thao tác (nguồn dùng transaction).
    public async Task<DeleteGroupResult> DeleteGroupAsync(string groupCode)
    {
        groupCode = (groupCode ?? "").Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == groupCode);
        if (g is null) return new DeleteGroupResult(false, "group_not_found", groupCode, 0);

        // Dọn thành viên nhóm (Sys_UserInGroup_Delete_ByGroup).
        var members = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.GroupCode == groupCode).ToListAsync();
        db.SysUserInGroups.RemoveRange(members);

        // Xóa nhóm (Sys_Group).
        db.SysGroups.Remove(g);
        await db.SaveChangesAsync();

        return new DeleteGroupResult(true, "ok", groupCode, members.Count);
    }

    // Sys_UserInGroup_Save (nguồn 2010.HTC): thay TOÀN BỘ thành viên của nhóm trong 1 thao tác
    // (xóa hết thành viên cũ rồi ghi danh sách mới). Trả null nếu nhóm không tồn tại.
    public async Task<object?> SetGroupMembersAsync(string groupCode, List<string> userCodes)
    {
        groupCode = groupCode.Trim().ToUpperInvariant();
        if (!await db.SysGroups.AnyAsync(x => x.OrgId == Org && x.GroupCode == groupCode)) return null;
        var old = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.GroupCode == groupCode).ToListAsync();
        db.SysUserInGroups.RemoveRange(old);
        var wanted = (userCodes ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim()).Distinct().ToList();
        foreach (var u in wanted)
            db.SysUserInGroups.Add(new SysUserInGroup { OrgId = Org, GroupCode = groupCode, UserCode = u });
        await db.SaveChangesAsync();
        return new { groupCode, members = wanted };
    }

    public async Task<object?> ListGroupMembersAsync(string groupCode)
    {
        groupCode = groupCode.Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == groupCode);
        if (g is null) return null;
        var members = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.GroupCode == groupCode)
            .Select(x => x.UserCode).OrderBy(x => x).ToListAsync();
        return new { g.GroupCode, g.GroupName, g.FlagActive, members };
    }

    // Các nhóm mà user thuộc về (Sys_UserInGroup) — cầu nối user → nhóm → Sys_Access.
    public async Task<object> GroupsOfUserAsync(string userCode)
    {
        userCode = userCode.Trim();
        var groups = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.UserCode == userCode)
            .Select(x => x.GroupCode).OrderBy(x => x).ToListAsync();
        return new { userCode, groups };
    }

    // ===== Sys_Access (nguồn 2010.HTC) =====
    // Sys_Access_Save: thay TOÀN BỘ grant object của nhóm trong 1 thao tác
    // (xóa hết grant cũ của nhóm rồi ghi danh sách object mới). Trả null nếu nhóm không tồn tại.
    // Chỉ nhận object ĐANG HOẠT ĐỘNG (Sys_Object.FlagActive) — giống điều kiện join trong Sys_Access_CheckDeny.
    public async Task<object?> SetGroupAccessAsync(string groupCode, List<string> objectCodes)
    {
        groupCode = groupCode.Trim().ToUpperInvariant();
        if (!await db.SysGroups.AnyAsync(x => x.OrgId == Org && x.GroupCode == groupCode)) return null;
        var old = await db.SysAccesses.Where(x => x.OrgId == Org && x.GroupCode == groupCode).ToListAsync();
        db.SysAccesses.RemoveRange(old);
        var wanted = (objectCodes ?? new List<string>())
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().ToUpperInvariant()).Distinct().ToList();
        var active = await db.SysObjects.Where(x => x.OrgId == Org && x.FlagActive && wanted.Contains(x.ObjectCode))
            .Select(x => x.ObjectCode).ToListAsync();
        foreach (var o in active)
            db.SysAccesses.Add(new SysAccess { OrgId = Org, GroupCode = groupCode, ObjectCode = o });
        await db.SaveChangesAsync();
        return new { groupCode, objects = active, skipped = wanted.Except(active).ToList() };
    }

    // GetAccess (nguồn 2010.HTC, SysAccessController.GetAccess + Sys_AccessService.ListObjectGet/List_SysAccess_Get):
    // Màn "Thêm chức năng cho nhóm" — trả về TOÀN BỘ danh mục object (Sys_Object_Get) kèm cờ granted
    // cho biết object đó đã được grant cho nhóm hay chưa (đối chiếu Sys_Access của nhóm).
    // Khác ListGroupAccessAsync (chỉ liệt kê object ĐÃ grant): đây là danh sách đầy đủ để tick chọn.
    // Trả null nếu nhóm không tồn tại.
    public async Task<object?> GetGroupAccessScreenAsync(string groupCode, string? type, bool? activeOnly)
    {
        groupCode = groupCode.Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == groupCode);
        if (g is null) return null;

        var granted = await db.SysAccesses.Where(x => x.OrgId == Org && x.GroupCode == groupCode)
            .Select(x => x.ObjectCode).ToListAsync();
        var grantedSet = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);

        var q = db.SysObjects.Where(x => x.OrgId == Org);
        if (!string.IsNullOrWhiteSpace(type)) { var t = type.Trim().ToUpperInvariant(); q = q.Where(x => x.ObjectType == t); }
        if (activeOnly == true) q = q.Where(x => x.FlagActive);
        var all = await q.OrderBy(x => x.ObjectCode)
            .Select(x => new { x.ObjectCode, x.ObjectName, x.ObjectType, x.ObjectCodeParent, x.FlagActive })
            .ToListAsync();

        var items = all.Select(o => new
        {
            o.ObjectCode, o.ObjectName, o.ObjectType, o.ObjectCodeParent, o.FlagActive,
            granted = grantedSet.Contains(o.ObjectCode)
        }).ToList();
        return new { g.GroupCode, g.GroupName, g.FlagActive, count = items.Count, grantedCount = grantedSet.Count, items };
    }

    // Sys_Access_Get: danh sách object mà nhóm được grant (kèm tên/loại object).
    public async Task<object?> ListGroupAccessAsync(string groupCode)
    {
        groupCode = groupCode.Trim().ToUpperInvariant();
        var g = await db.SysGroups.FirstOrDefaultAsync(x => x.OrgId == Org && x.GroupCode == groupCode);
        if (g is null) return null;
        var items = await db.SysAccesses.Where(x => x.OrgId == Org && x.GroupCode == groupCode)
            .Join(db.SysObjects.Where(o => o.OrgId == Org), a => a.ObjectCode, o => o.ObjectCode,
                (a, o) => new { o.ObjectCode, o.ObjectName, o.ObjectType, o.FlagActive })
            .OrderBy(x => x.ObjectCode).ToListAsync();
        return new { g.GroupCode, g.GroupName, g.FlagActive, count = items.Count, objects = items };
    }

    // ===== Sys_User_GetForCurrentUser (nguồn 2010.HTC) =====
    // Trả về hồ sơ user hiện tại (Sys_User + đội qua Sys_UserInTeam→Sys_UserTeam) và
    // danh sách object HIỆU LỰC = hợp của:
    //   (a) object đang hoạt động được grant qua các NHÓM đang hoạt động mà user thuộc về
    //       (Sys_UserInGroup → Sys_Group active → Sys_Access → Sys_Object active), và
    //   (b) nếu user có FlagSysAdmin='1' thì TẤT CẢ object đang hoạt động.
    // Giống đúng nhánh union trong Sys_User_GetForCurrentUser của nguồn.
    public async Task<object> GetForCurrentUserAsync(string userKey)
    {
        userKey = userKey.Trim();
        var profile = await db.SysUserProfiles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userKey);
        if (profile is null)
            return new { userKey, found = false, profile = (object?)null, team = (object?)null, objects = Array.Empty<object>() };

        // Đội của user (Sys_UserInTeam → Sys_UserTeam đang hoạt động).
        var team = await (from uit in db.SysUserInTeams.Where(x => x.OrgId == Org && x.UserCode == userKey)
                          join t in db.SysUserTeams.Where(x => x.OrgId == Org && x.FlagActive)
                              on new { uit.TeamCode, uit.DealerCode } equals new { t.TeamCode, t.DealerCode }
                          select new { t.TeamCode, t.DealerCode, t.TeamName }).FirstOrDefaultAsync();

        // (a) object hiệu lực qua nhóm đang hoạt động.
        var groupCodes = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.UserCode == userKey)
            .Select(x => x.GroupCode).ToListAsync();
        var activeGroups = await db.SysGroups.Where(x => x.OrgId == Org && x.FlagActive && groupCodes.Contains(x.GroupCode))
            .Select(x => x.GroupCode).ToListAsync();
        var granted = await db.SysAccesses.Where(x => x.OrgId == Org && activeGroups.Contains(x.GroupCode))
            .Select(x => x.ObjectCode).Distinct().ToListAsync();

        // (b) SysAdmin: tất cả object đang hoạt động.
        var isAdmin = profile.FlagSysAdmin;
        var effectiveCodes = isAdmin
            ? await db.SysObjects.Where(x => x.OrgId == Org && x.FlagActive).Select(x => x.ObjectCode).ToListAsync()
            : await db.SysObjects.Where(x => x.OrgId == Org && x.FlagActive && granted.Contains(x.ObjectCode))
                .Select(x => x.ObjectCode).ToListAsync();

        var objects = await db.SysObjects.Where(x => x.OrgId == Org && effectiveCodes.Contains(x.ObjectCode))
            .OrderBy(x => x.ObjectCode)
            .Select(x => new { x.ObjectCode, x.ObjectName, x.ObjectType, x.ObjectCodeParent, x.FlagActive })
            .ToListAsync();

        return new
        {
            userKey,
            found = true,
            profile = new { profile.UserCode, profile.DealerCode, profile.DeptCode, profile.UserName, profile.ViewAbilityType, profile.FlagSysAdmin, profile.FlagActive },
            team,
            isSysAdmin = isAdmin,
            count = objects.Count,
            objects
        };
    }

    // ===== Sys_User_GetByViewAbility (nguồn 2010.HTC) =====
    // DealerCodeRoot = 'HTC' (TConst.BizMix.DealerCodeRoot). ViewAbilityType: ADMIN/ALL/TEAM/USER.
    private const string DealerCodeRoot = "HTC";

    // myCache_Sys_User_ViewAbility_Get: tính tập user được XEM (read) và được GHI (write) của user hiện tại.
    // Phân nhánh giống nguồn: SysAdmin > (DealerRoot|DealerChild) x (ADMIN|ALL|TEAM|USER).
    //  - SysAdmin: read = write = TẤT CẢ user.
    //  - DealerRoot + ADMIN: read = user thuộc đại lý có DealerBUCode khớp DealerBUPattern; write = cùng DealerCode.
    //  - DealerRoot + ALL:   read = theo DealerBUPattern; write = chính mình.
    //  - DealerRoot + TEAM:  read = chính mình ∪ thành viên cùng đội (đội đang hoạt động); write = chính mình.
    //  - DealerRoot + USER:  read = write = chính mình.
    //  - DealerChild + ADMIN: read = write = user cùng DealerCode.
    //  - DealerChild + ALL:   read = user cùng DealerCode; write = chính mình.
    //  - DealerChild + TEAM:  read = chính mình ∪ thành viên cùng đội; write = chính mình.
    //  - DealerChild + USER:  read = write = chính mình.
    public async Task<object> GetByViewAbilityAsync(string userKey)
    {
        var (found, scope, type, dealerCode, read, write) = await ComputeViewAbilityAsync(userKey);
        if (!found) return new { userKey = userKey.Trim(), found = false, read = Array.Empty<string>(), write = Array.Empty<string>(), scope = "none" };
        return new { userKey = userKey.Trim(), found = true, scope, viewAbilityType = type, dealerCode, read, write };
    }

    private async Task<(bool found, string scope, string type, string dealerCode, List<string> read, List<string> write)> ComputeViewAbilityAsync(string userKey)
    {
        userKey = userKey.Trim();
        var me = await db.SysUserViewAbilities.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userKey && x.FlagActive);
        if (me is null) return (false, "none", "", "", new List<string>(), new List<string>());

        var all = await db.SysUserViewAbilities.Where(x => x.OrgId == Org && x.FlagActive).ToListAsync();
        var type = (me.ViewAbilityType ?? "USER").Trim().ToUpperInvariant();
        var isRoot = string.Equals(me.DealerCode, DealerCodeRoot, StringComparison.OrdinalIgnoreCase);
        List<string> read, write; string scope;

        if (me.FlagSysAdmin)
        {
            scope = "sysadmin";
            read = all.Select(x => x.UserCode).Distinct().ToList();
            write = read.ToList();
        }
        else if (isRoot && type == "ADMIN")
        {
            // read theo DealerBUPattern (mẫu mã BU của đại lý); write cùng DealerCode.
            scope = "dealerroot.admin";
            read = all.Where(x => MatchesBuPattern(x.DealerBUPattern, me.DealerBUPattern)).Select(x => x.UserCode).Distinct().ToList();
            write = all.Where(x => x.DealerCode == me.DealerCode).Select(x => x.UserCode).Distinct().ToList();
        }
        else if (isRoot && type == "ALL")
        {
            scope = "dealerroot.all";
            read = all.Where(x => MatchesBuPattern(x.DealerBUPattern, me.DealerBUPattern)).Select(x => x.UserCode).Distinct().ToList();
            write = new List<string> { me.UserCode };
        }
        else if (isRoot && type == "TEAM")
        {
            scope = "dealerroot.team";
            read = await TeamMembersAsync(me.UserCode);
            write = new List<string> { me.UserCode };
        }
        else if (isRoot && type == "USER")
        {
            scope = "dealerroot.user";
            read = new List<string> { me.UserCode };
            write = new List<string> { me.UserCode };
        }
        else if (!isRoot && type == "ADMIN")
        {
            scope = "dealerchild.admin";
            read = all.Where(x => x.DealerCode == me.DealerCode).Select(x => x.UserCode).Distinct().ToList();
            write = read.ToList();
        }
        else if (!isRoot && type == "ALL")
        {
            scope = "dealerchild.all";
            read = all.Where(x => x.DealerCode == me.DealerCode).Select(x => x.UserCode).Distinct().ToList();
            write = new List<string> { me.UserCode };
        }
        else if (!isRoot && type == "TEAM")
        {
            scope = "dealerchild.team";
            read = await TeamMembersAsync(me.UserCode);
            write = new List<string> { me.UserCode };
        }
        else
        {
            scope = "dealerchild.user";
            read = new List<string> { me.UserCode };
            write = new List<string> { me.UserCode };
        }
        return (true, scope, type, me.DealerCode, read, write);
    }

    // read của nhánh TEAM: chính mình ∪ thành viên cùng đội (đội đang hoạt động) — giống union trong nguồn.
    private async Task<List<string>> TeamMembersAsync(string userCode)
    {
        var myTeams = await db.SysUserInTeams.Where(x => x.OrgId == Org && x.UserCode == userCode)
            .Select(x => new { x.TeamCode, x.DealerCode }).ToListAsync();
        var result = new HashSet<string> { userCode };
        foreach (var t in myTeams)
        {
            var active = await db.SysUserTeams.AnyAsync(x => x.OrgId == Org && x.FlagActive && x.TeamCode == t.TeamCode && x.DealerCode == t.DealerCode);
            if (!active) continue;
            var members = await db.SysUserInTeams.Where(x => x.OrgId == Org && x.TeamCode == t.TeamCode && x.DealerCode == t.DealerCode)
                .Select(x => x.UserCode).ToListAsync();
            foreach (var m in members) result.Add(m);
        }
        return result.OrderBy(x => x).ToList();
    }

    // So khớp DealerBUCode với DealerBUPattern kiểu SQL LIKE (mẫu có %).
    private static bool MatchesBuPattern(string? dealerBuCode, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "%") return true;
        var p = pattern.Trim();
        var code = (dealerBuCode ?? "").Trim();
        if (p.StartsWith('%') && p.EndsWith('%') && p.Length >= 2)
            return code.Contains(p.Substring(1, p.Length - 2), StringComparison.OrdinalIgnoreCase);
        if (p.EndsWith('%')) return code.StartsWith(p.Substring(0, p.Length - 1), StringComparison.OrdinalIgnoreCase);
        if (p.StartsWith('%')) return code.EndsWith(p.Substring(1), StringComparison.OrdinalIgnoreCase);
        return string.Equals(code, p, StringComparison.OrdinalIgnoreCase);
    }

    // myCache_ViewAbility_CheckAccessUser: user hiện tại có quyền GHI lên targetUserCode không.
    // Dùng tập write của GetByViewAbility; SysAdmin luôn được phép.
    public async Task<object> CheckAccessUserAsync(string userKey, string targetUserCode)
    {
        userKey = userKey.Trim(); targetUserCode = targetUserCode.Trim();
        var me = await db.SysUserViewAbilities.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userKey && x.FlagActive);
        if (me is null) return new { userKey, targetUserCode, allowed = false, reason = "user_not_found" };
        if (me.FlagSysAdmin) return new { userKey, targetUserCode, allowed = true, reason = "sysadmin" };
        var (_, _, _, _, _, write) = await ComputeViewAbilityAsync(userKey);
        var allowed = write.Contains(targetUserCode);
        return new { userKey, targetUserCode, allowed, reason = allowed ? "in_write_scope" : "denied" };
    }

    // ===== Sys_User_Login (nguồn 2010.HTC) =====
    // Đăng nhập: (1) Sys_User_CheckDB — user phải TỒN TẠI và ĐANG HOẠT ĐỘNG (FlagActive);
    // (2) Mst_Dealer_CheckDB — đại lý của user phải TỒN TẠI và ĐANG HOẠT ĐỘNG;
    // (3) so khớp mật khẩu (Sys_User.UserPassword). Sai bước nào trả reason tương ứng.
    public async Task<object> LoginAsync(string userCode, string password)
    {
        userCode = userCode.Trim();
        var user = await db.SysUserProfiles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userCode);
        if (user is null) return new { userCode, ok = false, reason = "user_not_found" };
        if (!user.FlagActive) return new { userCode, ok = false, reason = "user_inactive" };

        var dealer = await db.MstDealers.FirstOrDefaultAsync(x => x.OrgId == Org && x.DealerCode == user.DealerCode);
        if (dealer is null) return new { userCode, ok = false, reason = "dealer_not_found" };
        if (!dealer.FlagActive) return new { userCode, ok = false, reason = "dealer_inactive" };

        if (!string.Equals(password ?? "", user.UserPassword, StringComparison.Ordinal))
            return new { userCode, ok = false, reason = "invalid_password" };

        return new
        {
            userCode,
            ok = true,
            reason = "ok",
            user = new { user.UserCode, user.DealerCode, user.DeptCode, user.UserName, user.ViewAbilityType, user.FlagSysAdmin },
            dealer = new { dealer.DealerCode, dealer.DealerName }
        };
    }

    // ===== Sys_User_ChangePassword (nguồn 2010.HTC) =====
    // Đổi mật khẩu: user phải TỒN TẠI và ĐANG HOẠT ĐỘNG; mật khẩu cũ phải khớp; sau đó ghi mật khẩu mới.
    public async Task<object> ChangePasswordAsync(string userCode, string oldPassword, string newPassword)
    {
        userCode = userCode.Trim();
        var user = await db.SysUserProfiles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userCode);
        if (user is null) return new { userCode, ok = false, reason = "user_not_found" };
        if (!user.FlagActive) return new { userCode, ok = false, reason = "user_inactive" };
        if (!string.Equals(oldPassword ?? "", user.UserPassword, StringComparison.Ordinal))
            return new { userCode, ok = false, reason = "invalid_password_old" };
        if (string.IsNullOrWhiteSpace(newPassword))
            return new { userCode, ok = false, reason = "new_password_empty" };

        user.UserPassword = newPassword;
        await db.SaveChangesAsync();
        return new { userCode, ok = true, reason = "ok" };
    }

    // ===== Sys_User_Create (nguồn 2010.HTC) =====
    // Tạo hồ sơ user mới kèm kiểm tra ràng buộc giống nguồn:
    //  (1) UserCode bắt buộc; (2) UserCode CHƯA tồn tại (Sys_User_CheckDB, Flag.Inactive);
    //  (3) DealerCode phải TỒN TẠI (Mst_Dealer_CheckDB, Flag.Yes);
    //  (4) DeptCode phải TỒN TẠI và ĐANG HOẠT ĐỘNG theo đại lý (Mst_Department_CheckDB);
    //  (5) UserStaffId (nếu có) phải DUY NHẤT theo đại lý; (6) ViewAbilityType bắt buộc;
    //  (7) UserName bắt buộc; (8) UserPassword bắt buộc. Ghi với FlagActive='1'.
    public async Task<object> CreateUserAsync(CreateUserDto d)
    {
        var userCode = (d.UserCode ?? "").Trim();
        var dealerCode = (d.DealerCode ?? "").Trim().ToUpperInvariant();
        var deptCode = (d.DeptCode ?? "").Trim().ToUpperInvariant();
        var staffId = (d.UserStaffId ?? "").Trim();
        var userName = (d.UserName ?? "").Trim();
        var password = d.UserPassword ?? "";
        var viewAbilityType = (d.ViewAbilityType ?? "").Trim().ToUpperInvariant();

        if (userCode.Length == 0) return new { ok = false, reason = "invalid_usercode" };
        if (await db.SysUserProfiles.AnyAsync(x => x.OrgId == Org && x.UserCode == userCode))
            return new { ok = false, reason = "usercode_exist", userCode };
        if (!await db.MstDealers.AnyAsync(x => x.OrgId == Org && x.DealerCode == dealerCode))
            return new { ok = false, reason = "dealer_not_found", dealerCode };
        if (!await db.MstDepartments.AnyAsync(x => x.OrgId == Org && x.DeptCode == deptCode && x.DealerCode == dealerCode && x.FlagActive))
            return new { ok = false, reason = "department_not_found", deptCode, dealerCode };
        if (staffId.Length > 0 && await db.SysUserProfiles.AnyAsync(x => x.OrgId == Org && x.DealerCode == dealerCode && x.UserStaffId == staffId))
            return new { ok = false, reason = "staffid_exist", userStaffId = staffId, dealerCode };
        if (viewAbilityType.Length == 0) return new { ok = false, reason = "invalid_viewabilitytype" };
        if (userName.Length == 0) return new { ok = false, reason = "invalid_username" };
        if (password.Length == 0) return new { ok = false, reason = "invalid_userpassword" };

        var u = new SysUserProfile
        {
            OrgId = Org,
            UserCode = userCode,
            DealerCode = dealerCode,
            DeptCode = deptCode,
            UserStaffId = staffId,
            UserName = userName,
            UserPassword = password,
            UserEmail = (d.UserEmail ?? "").Trim(),
            UserPhoneNo = (d.UserPhoneNo ?? "").Trim(),
            ViewAbilityType = viewAbilityType,
            FlagSysAdmin = d.FlagSysAdmin ?? false,
            FlagActive = true
        };
        db.SysUserProfiles.Add(u);
        await db.SaveChangesAsync();
        return new { ok = true, reason = "ok", user = new { u.UserCode, u.DealerCode, u.DeptCode, u.UserStaffId, u.UserName, u.ViewAbilityType, u.FlagSysAdmin, u.FlagActive } };
    }

    // ===== Sys_User_Update (nguồn 2010.HTC) =====
    // Cập nhật hồ sơ user (partial theo Ft_Cols_Upd) kèm kiểm tra ràng buộc giống nguồn:
    //  (1) Sys_User_CheckDB(Flag.Yes): user phải TỒN TẠI, nếu không trả reason user_not_found;
    //  (2) DealerCode KHÔNG cập nhật — lấy từ DB (nguồn gán lại strDealerCode = dtDB_Sys_User.DealerCode);
    //  (3) nếu cập nhật ViewAbilityType thì phải KHÁC RỖNG (Sys_User_Update_InvalidViewAbilityType);
    //  (4) nếu cập nhật UserName thì phải KHÁC RỖNG (Sys_User_Update_InvalidUserName);
    //  (5) nếu cập nhật UserStaffId và khác rỗng thì phải DUY NHẤT theo đại lý
    //      (Sys_User_Update_InvalidUserStaffId) — bỏ qua chính user đang sửa.
    // Chỉ ghi các cột có trong Cols (Ft_Cols_Upd); Cols rỗng/null = cập nhật tất cả cột cho phép.
    public async Task<object> UpdateUserAsync(string userCode, UpdateUserDto d)
    {
        userCode = (userCode ?? "").Trim();
        var user = await db.SysUserProfiles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userCode);
        if (user is null) return new { ok = false, reason = "user_not_found", userCode };

        // Ft_Cols_Upd: danh sách cột cần cập nhật (rỗng = tất cả). So khớp không phân biệt hoa/thường.
        var cols = (d.Cols ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
        bool Upd(string col) => cols.Count == 0 || cols.Contains(col.ToUpperInvariant());

        var staffId = (d.UserStaffId ?? "").Trim();
        var userName = (d.UserName ?? "").Trim();
        var viewAbilityType = (d.ViewAbilityType ?? "").Trim().ToUpperInvariant();

        // (3) ViewAbilityType bắt buộc khi cập nhật.
        if (Upd("ViewAbilityType") && viewAbilityType.Length == 0)
            return new { ok = false, reason = "invalid_viewabilitytype", userCode };
        // (4) UserName bắt buộc khi cập nhật.
        if (Upd("UserName") && userName.Length == 0)
            return new { ok = false, reason = "invalid_username", userCode };
        // (5) UserStaffId duy nhất theo đại lý (bỏ qua chính user đang sửa).
        if (Upd("UserStaffId") && staffId.Length > 0
            && await db.SysUserProfiles.AnyAsync(x => x.OrgId == Org && x.DealerCode == user.DealerCode && x.UserStaffId == staffId && x.UserCode != userCode))
            return new { ok = false, reason = "staffid_exist", userStaffId = staffId, dealerCode = user.DealerCode };

        // Ghi các cột được chọn (DealerCode/DeptCode KHÔNG cập nhật — giống nguồn).
        if (Upd("UserStaffId")) user.UserStaffId = staffId;
        if (Upd("UserName")) user.UserName = userName;
        if (Upd("UserPassword")) user.UserPassword = d.UserPassword ?? "";
        if (Upd("UserEmail")) user.UserEmail = (d.UserEmail ?? "").Trim();
        if (Upd("UserPhoneNo")) user.UserPhoneNo = (d.UserPhoneNo ?? "").Trim();
        if (Upd("ViewAbilityType")) user.ViewAbilityType = viewAbilityType;
        if (Upd("FlagSysAdmin")) user.FlagSysAdmin = d.FlagSysAdmin ?? false;
        if (Upd("FlagActive")) user.FlagActive = d.FlagActive ?? true;

        await db.SaveChangesAsync();
        return new { ok = true, reason = "ok", user = new { user.UserCode, user.DealerCode, user.DeptCode, user.UserStaffId, user.UserName, user.UserEmail, user.UserPhoneNo, user.ViewAbilityType, user.FlagSysAdmin, user.FlagActive } };
    }

    // ===== Sys_User_Delete (nguồn 2010.HTC) =====
    // Xóa hồ sơ user kèm dọn dẹp liên kết giống nguồn:
    //  (1) Sys_User_CheckDB(Flag.Yes): user phải TỒN TẠI, nếu không trả reason user_not_found;
    //  (2) Sys_UserInGroup_Delete_ByUser: xóa mọi dòng Sys_UserInGroup của user;
    //  (3) Sys_UserInTeam_Delete_ByUser: xóa mọi dòng Sys_UserInTeam của user;
    //  (4) xóa dòng Sys_User. Toàn bộ trong 1 thao tác (nguồn dùng transaction).
    public async Task<DeleteUserResult> DeleteUserAsync(string userCode)
    {
        userCode = (userCode ?? "").Trim();
        var user = await db.SysUserProfiles.FirstOrDefaultAsync(x => x.OrgId == Org && x.UserCode == userCode);
        if (user is null) return new DeleteUserResult(false, "user_not_found", userCode, 0, 0);

        // Dọn thành viên nhóm (Sys_UserInGroup_Delete_ByUser).
        var groups = await db.SysUserInGroups.Where(x => x.OrgId == Org && x.UserCode == userCode).ToListAsync();
        db.SysUserInGroups.RemoveRange(groups);

        // Dọn thành viên đội (Sys_UserInTeam_Delete_ByUser).
        var teams = await db.SysUserInTeams.Where(x => x.OrgId == Org && x.UserCode == userCode).ToListAsync();
        db.SysUserInTeams.RemoveRange(teams);

        // Xóa hồ sơ user (Sys_User).
        db.SysUserProfiles.Remove(user);
        await db.SaveChangesAsync();

        return new DeleteUserResult(true, "ok", userCode, groups.Count, teams.Count);
    }
}
