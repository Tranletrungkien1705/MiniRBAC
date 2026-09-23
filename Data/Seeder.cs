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
        await db.SaveChangesAsync();
    }
}
