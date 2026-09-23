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
