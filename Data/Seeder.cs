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
        // Cấu hình chức năng gắn với đối tượng (SysObjectSetting nguồn 2010.HTC) — dùng cho ResolveObjects.
        // Dữ liệu THẬT lấy từ HCare.idocNet.App/Resources/SysObjectSetting.config (mã object + FUNC thật).
        if (!await db.SysObjectFunctions.AnyAsync())
        {
            db.SysObjectFunctions.AddRange(
                new SysObjectFunction { OrgId = org, ObjectCode = "AUTH_SYSGROUP", FunctionCodes = "Sys_Group_Create,Sys_Group_Delete,Sys_Group_Get,Sys_Group_Update,Sys_UserInGroup_Save" },
                new SysObjectFunction { OrgId = org, ObjectCode = "AUTH_SYSOBJECT", FunctionCodes = "Sys_Access_Get,Sys_Access_Save" },
                new SysObjectFunction { OrgId = org, ObjectCode = "AUTH_SYSUSER", FunctionCodes = "Sys_User_ChangePassword,Sys_User_Create,Sys_User_Delete,Sys_User_Get,Sys_User_GetForCurrentUser,Sys_User_Login,Sys_User_Update" },
                new SysObjectFunction { OrgId = org, ObjectCode = "AUTH_SYSUSERINGROUP", FunctionCodes = "Sys_UserInGroup_Save" },
                new SysObjectFunction { OrgId = org, ObjectCode = "ADM_ACCOUNT", FunctionCodes = "Mst_Account_CreateMulti,Mst_Account_Delete,Mst_Account_Get,Mst_Account_Update" },
                new SysObjectFunction { OrgId = org, ObjectCode = "ADM_ACCTYPE", FunctionCodes = "Mst_AccType_Create,Mst_AccType_Delete,Mst_AccType_Get,Mst_AccType_Update" },
                new SysObjectFunction { OrgId = org, ObjectCode = "ADM_MODEL", FunctionCodes = "Mst_CarModel_Create,Mst_CarModel_CreateMulti,Mst_CarModel_Delete,Mst_CarModel_Get,Mst_CarModel_Update" });
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
        // Hồ sơ user (Sys_User) + thành viên đội (Sys_UserInTeam nguồn 2010.HTC) — dùng cho Sys_User_GetForCurrentUser.
        // Nguồn KHÔNG có script seed cho 2 bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột,
        // khớp user/đội đã seed ở trên (admin@demo, leader01@demo, sale01@demo; TEAM01/DL01).
        if (!await db.SysUserProfiles.AnyAsync())
        {
            db.SysUserProfiles.AddRange(
                new SysUserProfile { OrgId = org, UserCode = "admin@demo", DealerCode = "DL01", DeptCode = "ROOT", UserName = "Quản trị demo", UserPassword = "admin123", ViewAbilityType = "ALL", AreaCode = "VN", FlagSysAdmin = true },
                new SysUserProfile { OrgId = org, UserCode = "leader01@demo", DealerCode = "DL01", DeptCode = "SALES", UserName = "Trưởng đội 1", UserPassword = "leader123", ViewAbilityType = "TEAM", AreaCode = "MB" },
                new SysUserProfile { OrgId = org, UserCode = "sale01@demo", DealerCode = "DL01", DeptCode = "SALES", UserName = "Nhân viên bán hàng 1", UserPassword = "sale123", ViewAbilityType = "SELF", AreaCode = "MB" });
        }
        if (!await db.SysUserInTeams.AnyAsync())
        {
            db.SysUserInTeams.AddRange(
                new SysUserInTeam { OrgId = org, UserCode = "leader01@demo", TeamCode = "TEAM01", DealerCode = "DL01" },
                new SysUserInTeam { OrgId = org, UserCode = "sale01@demo", TeamCode = "TEAM01", DealerCode = "DL01" });
        }
        // Khả năng nhìn dữ liệu (Sys_User_GetAbilityViewOfUser nguồn 2010.HTC) — dùng cho Sys_User_GetByViewAbility.
        // Nguồn KHÔNG có script seed cho bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột,
        // khớp user đã seed ở trên. DealerCodeRoot = 'HTC' (TConst.BizMix.DealerCodeRoot).
        if (!await db.SysUserViewAbilities.AnyAsync())
        {
            db.SysUserViewAbilities.AddRange(
                new SysUserViewAbility { OrgId = org, UserCode = "admin@demo", DealerCode = "HTC", DealerBUPattern = "%", ViewAbilityType = "ADMIN", FlagSysAdmin = true },
                new SysUserViewAbility { OrgId = org, UserCode = "leader01@demo", DealerCode = "DL01", DealerBUPattern = "DL01%", ViewAbilityType = "TEAM" },
                new SysUserViewAbility { OrgId = org, UserCode = "sale01@demo", DealerCode = "DL01", DealerBUPattern = "DL01%", ViewAbilityType = "USER" });
        }
        // Đại lý (Mst_Dealer nguồn 2010.HTC) — dùng cho Sys_User_Login (Mst_Dealer_CheckDB).
        // Nguồn KHÔNG có script seed cho bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột,
        // khớp DealerCode của user đã seed ở trên (DL01, DL02).
        if (!await db.MstDealers.AnyAsync())
        {
            db.MstDealers.AddRange(
                new MstDealer { OrgId = org, DealerCode = "DL01", DealerName = "Đại lý 1", DealerBUCode = "DL01", DealerBUPattern = "DL01%" },
                new MstDealer { OrgId = org, DealerCode = "DL02", DealerName = "Đại lý 2", DealerBUCode = "DL02", DealerBUPattern = "DL02%" });
        }
        // Phòng ban (Mst_Department nguồn 2010.HTC) — dùng cho Sys_User_Create (Mst_Department_CheckDB).
        // Nguồn KHÔNG có script seed cho bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột,
        // khớp DealerCode của user đã seed ở trên (DL01, DL02).
        if (!await db.MstDepartments.AnyAsync())
        {
            db.MstDepartments.AddRange(
                new MstDepartment { OrgId = org, DeptCode = "ROOT", DealerCode = "DL01", DeptName = "Ban giám đốc" },
                new MstDepartment { OrgId = org, DeptCode = "SALES", DealerCode = "DL01", DeptName = "Phòng kinh doanh" },
                new MstDepartment { OrgId = org, DeptCode = "ROOT", DealerCode = "DL02", DeptName = "Ban giám đốc" });
        }
        // Vùng thị trường (Mst_AreaMarket nguồn 2010.HTC) — dùng cho engine phạm vi nhìn dữ liệu theo vùng.
        // Nguồn KHÔNG có script seed cho bảng này nên dùng dữ liệu minh họa theo đúng mô hình cột
        // (AreaCode/AreaCodeParent/AreaBUCode/AreaBUPattern/AreaStatus). AreaBUCode/AreaBUPattern theo
        // đúng quy tắc Mst_AreaMarket_UpdBU: AreaBUCode = cha.AreaBUCode + '.' + AreaCode, AreaBUPattern = AreaBUCode + '%'.
        if (!await db.MstAreaMarkets.AnyAsync())
        {
            db.MstAreaMarkets.AddRange(
                new MstAreaMarket { OrgId = org, AreaCode = "VN", AreaName = "Toàn quốc", AreaBUCode = "VN", AreaBUPattern = "VN%", AreaLevel = 1, AreaStatus = "1" },
                new MstAreaMarket { OrgId = org, AreaCode = "MB", AreaName = "Miền Bắc", AreaCodeParent = "VN", AreaBUCode = "VN.MB", AreaBUPattern = "VN.MB%", AreaLevel = 2, AreaStatus = "1" },
                new MstAreaMarket { OrgId = org, AreaCode = "MN", AreaName = "Miền Nam", AreaCodeParent = "VN", AreaBUCode = "VN.MN", AreaBUPattern = "VN.MN%", AreaLevel = 2, AreaStatus = "1" });
        }
        // Phiên đăng nhập (Session nguồn 2010.HTC) — dùng cho Sys_User_Logout.
        // Nguồn KHÔNG có script seed cho bảng này (phiên sinh động khi login) nên dùng 1 phiên
        // minh họa đang hoạt động, khớp user đã seed ở trên (admin@demo).
        if (!await db.SysUserSessions.AnyAsync())
        {
            db.SysUserSessions.Add(
                new SysUserSession { OrgId = org, SessionId = "sess_demo_admin", UserCode = "admin@demo", LoginAt = DateTime.Now, FlagActive = true });
        }
        await db.SaveChangesAsync();
    }
}
