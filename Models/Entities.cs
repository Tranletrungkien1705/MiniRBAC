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
