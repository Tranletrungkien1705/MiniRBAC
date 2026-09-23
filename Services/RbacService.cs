using Microsoft.EntityFrameworkCore;
using MiniRBAC.Data;
using MiniRBAC.Models;

namespace MiniRBAC.Services;

public record CodeNameDto(string Code, string Name, string? Module);
public record GrantDto(string PermissionCode);
public record AssignDto(string RoleCode);
public record SysObjectDto(string ObjectCode, string ObjectName, string? ObjectType, string? ObjectCodeParent, bool? FlagActive);

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
}
