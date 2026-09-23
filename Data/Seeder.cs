using Microsoft.EntityFrameworkCore;
using MiniRBAC.Models;
namespace MiniRBAC.Data;
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        var org = TenantContext.DefaultOrgId;
        if (!await db.Orgs.AnyAsync(o => o.Id == org))
            db.Orgs.Add(new Org { Id = org, Name = "Demo RBAC", ApiKey = "demo-rbac" });
        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(
                new Role { OrgId = org, Code = "ADMIN", Name = "Quản trị" },
                new Role { OrgId = org, Code = "DEALER", Name = "Đại lý" });
            db.Permissions.AddRange(
                new Permission { OrgId = org, Code = "VEHICLE.DELIVER", Name = "Giao xe", Module = "Vehicle" },
                new Permission { OrgId = org, Code = "PARTS.ORDER.CONFIRM", Name = "Duyệt đơn PT", Module = "Parts" },
                new Permission { OrgId = org, Code = "FORM.ALLOCATE", Name = "Cấp phôi", Module = "Form" });
            db.RolePermissions.AddRange(
                new RolePermission { OrgId = org, RoleCode = "ADMIN", PermissionCode = "VEHICLE.DELIVER" },
                new RolePermission { OrgId = org, RoleCode = "ADMIN", PermissionCode = "PARTS.ORDER.CONFIRM" },
                new RolePermission { OrgId = org, RoleCode = "ADMIN", PermissionCode = "FORM.ALLOCATE" },
                new RolePermission { OrgId = org, RoleCode = "DEALER", PermissionCode = "PARTS.ORDER.CONFIRM" });
        }
        // Danh mục Sys_Object (nguồn 2010.HTC): mã thật từ DBScript/Security/*.Security_Object.sql.
        if (!await db.SysObjects.AnyAsync())
        {
            db.SysObjects.AddRange(
                new SysObject { OrgId = org, ObjectCode = "MNU_SALES", ObjectName = "Menu Bán hàng", ObjectType = "MENU" },
                new SysObject { OrgId = org, ObjectCode = "MNU_SALES_CREATE_OD", ObjectName = "Menu Bán hàng - Đặt hàng", ObjectType = "MENU", ObjectCodeParent = "MNU_SALES" },
                new SysObject { OrgId = org, ObjectCode = "MNU_SALES_MNG_OD_HTC", ObjectName = "Menu Bán hàng - HTC QL đơn hàng", ObjectType = "MENU", ObjectCodeParent = "MNU_SALES" },
                new SysObject { OrgId = org, ObjectCode = "BTN_MNG_OD_HTC_APRV_1", ObjectName = "Mh Htc QL đơn hàng - Approve 1", ObjectType = "BTN", ObjectCodeParent = "MNU_SALES_MNG_OD_HTC" },
                new SysObject { OrgId = org, ObjectCode = "MNU_ADMIN", ObjectName = "Menu Quản trị", ObjectType = "MENU" },
                new SysObject { OrgId = org, ObjectCode = "MNU_ADMIN_USER", ObjectName = "Menu Quản trị - QL người dùng", ObjectType = "MENU", ObjectCodeParent = "MNU_ADMIN" },
                new SysObject { OrgId = org, ObjectCode = "MNU_ADMIN_GROUP_FUNCTION", ObjectName = "Menu Quản trị - Gán nhóm, chức năng", ObjectType = "MENU", ObjectCodeParent = "MNU_ADMIN" });
        }
        // Đội bán hàng (Sys_UserTeam nguồn 2010.HTC) + phạm vi dữ liệu user (Sys_User).
        // Nguồn KHÔNG có script seed cho 2 bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột.
        if (!await db.SysUserTeams.AnyAsync())
        {
            db.SysUserTeams.AddRange(
                new SysUserTeam { OrgId = org, TeamCode = "TEAM01", DealerCode = "DL01", TeamName = "Đội bán hàng 1" },
                new SysUserTeam { OrgId = org, TeamCode = "TEAM02", DealerCode = "DL01", TeamName = "Đội bán hàng 2" },
                new SysUserTeam { OrgId = org, TeamCode = "TEAM01", DealerCode = "DL02", TeamName = "Đội bán hàng 1 (CN2)" });
        }
        if (!await db.SysUserScopes.AnyAsync())
        {
            db.SysUserScopes.AddRange(
                new SysUserScope { OrgId = org, UserKey = "admin@demo", DealerCode = "DL01", DBCode = "ROOT", FlagSysAdmin = true },
                new SysUserScope { OrgId = org, UserKey = "leader01@demo", DealerCode = "DL01", DBCode = "ROOT", TeamCode = "TEAM01", FlagTeamLeader = true },
                new SysUserScope { OrgId = org, UserKey = "sale01@demo", DealerCode = "DL01", DBCode = "ROOT", TeamCode = "TEAM01", FlagSalesman = true });
        }
        // Nhóm quyền (Sys_Group) + thành viên nhóm (Sys_UserInGroup nguồn 2010.HTC).
        // Nguồn KHÔNG có script seed cho 2 bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột.
        if (!await db.SysGroups.AnyAsync())
        {
            db.SysGroups.AddRange(
                new SysGroup { OrgId = org, GroupCode = "GRP_SALES", GroupName = "Nhóm bán hàng" },
                new SysGroup { OrgId = org, GroupCode = "GRP_ADMIN", GroupName = "Nhóm quản trị" });
            db.SysUserInGroups.AddRange(
                new SysUserInGroup { OrgId = org, GroupCode = "GRP_SALES", UserCode = "sale01@demo" },
                new SysUserInGroup { OrgId = org, GroupCode = "GRP_SALES", UserCode = "leader01@demo" },
                new SysUserInGroup { OrgId = org, GroupCode = "GRP_ADMIN", UserCode = "admin@demo" });
        }
        // Quyền nhóm trên object (Sys_Access nguồn 2010.HTC) — dùng mã object thật đã seed ở trên.
        // Nguồn KHÔNG có script seed cho bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột.
        if (!await db.SysAccesses.AnyAsync())
        {
            db.SysAccesses.AddRange(
                new SysAccess { OrgId = org, GroupCode = "GRP_SALES", ObjectCode = "MNU_SALES" },
                new SysAccess { OrgId = org, GroupCode = "GRP_SALES", ObjectCode = "MNU_SALES_CREATE_OD" },
                new SysAccess { OrgId = org, GroupCode = "GRP_SALES", ObjectCode = "MNU_SALES_MNG_OD_HTC" },
                new SysAccess { OrgId = org, GroupCode = "GRP_SALES", ObjectCode = "BTN_MNG_OD_HTC_APRV_1" },
                new SysAccess { OrgId = org, GroupCode = "GRP_ADMIN", ObjectCode = "MNU_ADMIN" },
                new SysAccess { OrgId = org, GroupCode = "GRP_ADMIN", ObjectCode = "MNU_ADMIN_USER" },
                new SysAccess { OrgId = org, GroupCode = "GRP_ADMIN", ObjectCode = "MNU_ADMIN_GROUP_FUNCTION" });
        }
        await db.SaveChangesAsync();
    }
}
