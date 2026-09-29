using Microsoft.EntityFrameworkCore;
using MiniRBAC.Models;
namespace MiniRBAC.Data;
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        // 🔴 BẪY #28: EnsureCreated là NO-OP khi .db đã tồn tại ⇒ bảng/cột thêm
        // sau đó KHÔNG được tạo, build xanh mà chạy lên chết "no such table".
        await MigrateSqliteAuto.RunAsync(db);
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
        await db.SaveChangesAsync();
    }
}
