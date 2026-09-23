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
