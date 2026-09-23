namespace MiniRBAC.Models;

public sealed class Org
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>Vai trò (Sys_Access role).</summary>
public sealed class Role
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Quyền = mã chức năng/menu (Sys_Access). Mô hình ALLOW: chưa grant = chặn.</summary>
public sealed class Permission
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string Code { get; set; } = "";     // vd VEHICLE.DELIVER, PARTS.ORDER.CONFIRM
    public string Name { get; set; } = "";
    public string? Module { get; set; }
}

public sealed class RolePermission
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string RoleCode { get; set; } = "";
    public string PermissionCode { get; set; } = "";
}

public sealed class UserRole
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserKey { get; set; } = "";  // sub/email từ MiniSSO
    public string RoleCode { get; set; } = "";
}

/// <summary>
/// Danh mục đối tượng/chức năng (Sys_Object nguồn 2010.HTC).
/// ObjectType: MENU / SCR / BTN / WS / WSFUNC / APP (Sys_ObjectType).
/// ObjectCodeParent: mã cha để dựng cây menu/chức năng.
/// FlagActive: chỉ đối tượng đang hoạt động mới được cấp quyền (Sys_Access_CheckDeny).
/// </summary>
public sealed class SysObject
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string ObjectCode { get; set; } = "";   // vd MNU_SALES, BTN_MNG_OD_HTC_APRV_1
    public string ObjectName { get; set; } = "";
    public string ObjectType { get; set; } = "MENU"; // MENU/SCR/BTN/WS/WSFUNC/APP
    public string? ObjectCodeParent { get; set; }     // mã cha (rỗng = gốc)
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Cờ quản trị hệ thống của user (Sys_User.FlagSysAdmin nguồn 2010.HTC).
/// User có FlagSysAdmin='1' được phép mọi object đang hoạt động (bypass trong Sys_Access_CheckDeny).
/// </summary>
public sealed class SysUserFlag
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserKey { get; set; } = "";
    public bool FlagSysAdmin { get; set; }
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Đội/nhóm bán hàng (Sys_UserTeam nguồn 2010.HTC).
/// Khóa nghiệp vụ = (TeamCode, DealerCode). FlagActive: chỉ đội đang hoạt động mới dùng để phân quyền phạm vi.
/// </summary>
public sealed class SysUserTeam
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string TeamCode { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string TeamName { get; set; } = "";
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Phạm vi dữ liệu của user (Sys_User nguồn 2010.HTC) — dùng cho View/Write ability của Sys_UserTeam.
/// Các cờ: FlagSysAdmin, FlagDBAdmin, FlagTeamLeader, FlagSalesman; vị trí: DealerCode, DBCode, TeamCode.
/// </summary>
public sealed class SysUserScope
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserKey { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string DBCode { get; set; } = "";
    public string TeamCode { get; set; } = "";
    public bool FlagSysAdmin { get; set; }
    public bool FlagDBAdmin { get; set; }
    public bool FlagTeamLeader { get; set; }
    public bool FlagSalesman { get; set; }
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Nhóm quyền (Sys_Group nguồn 2010.HTC). Khóa nghiệp vụ = GroupCode.
/// FlagActive: chỉ nhóm đang hoạt động mới được tính khi kiểm tra quyền (Sys_Access_CheckDeny).
/// </summary>
public sealed class SysGroup
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string GroupCode { get; set; } = "";
    public string GroupName { get; set; } = "";
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Thành viên nhóm (Sys_UserInGroup nguồn 2010.HTC). Khóa nghiệp vụ = (GroupCode, UserCode).
/// Là cầu nối user → nhóm → Sys_Access → object trong Sys_Access_CheckDeny.
/// </summary>
public sealed class SysUserInGroup
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string GroupCode { get; set; } = "";
    public string UserCode { get; set; } = "";
}

/// <summary>
/// Quyền của nhóm trên đối tượng (Sys_Access nguồn 2010.HTC). Khóa nghiệp vụ = (GroupCode, ObjectCode).
/// Là mắt nối nhóm → object trong Sys_Access_CheckDeny: user thuộc nhóm đang hoạt động có grant object
/// đang hoạt động thì được phép. Thao tác Sys_Access_Save thay TOÀN BỘ grant của nhóm trong 1 lần.
/// </summary>
public sealed class SysAccess
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string GroupCode { get; set; } = "";
    public string ObjectCode { get; set; } = "";
}

/// <summary>
/// Hồ sơ người dùng (Sys_User nguồn 2010.HTC) — dùng cho Sys_User_GetForCurrentUser.
/// Khóa nghiệp vụ = UserCode. ViewAbilityType: tầm nhìn dữ liệu; FlagSysAdmin: bypass deny-check.
/// UserPassword: mật khẩu đăng nhập (Sys_User.UserPassword) — dùng cho Sys_User_Login / Sys_User_ChangePassword.
/// </summary>
public sealed class SysUserProfile
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserCode { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string DeptCode { get; set; } = "";
    public string UserStaffId { get; set; } = "";   // mã nhân viên (Sys_User.UserStaffId) — duy nhất theo đại lý
    public string UserName { get; set; } = "";
    public string UserPassword { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string UserPhoneNo { get; set; } = "";
    public string? ViewAbilityType { get; set; }
    public bool FlagSysAdmin { get; set; }
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Phòng ban (Mst_Department nguồn 2010.HTC) — dùng cho Sys_User_Create (Mst_Department_CheckDB).
/// Khóa nghiệp vụ = (DeptCode, DealerCode). FlagActive: chỉ phòng ban đang hoạt động mới cho gán user.
/// </summary>
public sealed class MstDepartment
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string DeptCode { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string DeptName { get; set; } = "";
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Đại lý (Mst_Dealer nguồn 2010.HTC) — dùng cho Sys_User_Login (Mst_Dealer_CheckDB).
/// Khóa nghiệp vụ = DealerCode. FlagActive: chỉ đại lý đang hoạt động mới cho user đăng nhập.
/// DealerBUCode/DealerBUPattern: mã/mẫu BU của đại lý (dùng cho phạm vi nhìn dữ liệu).
/// </summary>
public sealed class MstDealer
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string DealerCode { get; set; } = "";
    public string DealerName { get; set; } = "";
    public string? DealerBUCode { get; set; }
    public string? DealerBUPattern { get; set; }
    public bool FlagActive { get; set; } = true;
}

/// <summary>
/// Thành viên đội (Sys_UserInTeam nguồn 2010.HTC). Khóa nghiệp vụ = (UserCode, TeamCode, DealerCode).
/// Là cầu nối user → đội trong Sys_User_GetForCurrentUser (lấy TeamCode/TeamName của user hiện tại).
/// </summary>
public sealed class SysUserInTeam
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserCode { get; set; } = "";
    public string TeamCode { get; set; } = "";
    public string DealerCode { get; set; } = "";
}

/// <summary>
/// Khả năng nhìn dữ liệu của user (Sys_User_GetAbilityViewOfUser nguồn 2010.HTC).
/// Khóa nghiệp vụ = UserCode. Là "ability of user" dùng để tính tập user được XEM/GHI
/// trong Sys_User_GetByViewAbility. ViewAbilityType: ADMIN/ALL/TEAM/USER (TConst.ViewAbilityType).
/// DealerCode = 'HTC' (DealerCodeRoot) là gốc; DealerBUPattern = mẫu mã BU của đại lý (Mst_Dealer.DealerBUPattern)
/// dùng để mở rộng phạm vi xem theo BU khi user ở đại lý gốc.
/// </summary>
public sealed class SysUserViewAbility
{
    public long Id { get; set; }
    public Guid OrgId { get; set; }
    public string UserCode { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string DealerBUPattern { get; set; } = "";
    public string ViewAbilityType { get; set; } = "USER"; // ADMIN/ALL/TEAM/USER
    public bool FlagSysAdmin { get; set; }
    public bool FlagActive { get; set; } = true;
}
