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
    // Sys_UserTeam + phạm vi dữ liệu (nguồn 2010.HTC)
    Task<object> AddTeamAsync(UserTeamDto d);
    Task<object> ListTeamsAsync(string? dealerCode, bool? activeOnly);
    Task<object> SetUserScopeAsync(UserScopeDto d);
    Task<object> ViewAbilityAsync(string userKey);
    // Sys_Group + Sys_UserInGroup (nguồn 2010.HTC)
    Task<object> AddGroupAsync(GroupDto d);
    Task<object> ListGroupsAsync(bool? activeOnly);
    Task<object?> SetGroupMembersAsync(string groupCode, List<string> userCodes);
    Task<object?> ListGroupMembersAsync(string groupCode);
    Task<object> GroupsOfUserAsync(string userCode);
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
}
