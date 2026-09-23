using Microsoft.EntityFrameworkCore;
using MiniRBAC.Models;
namespace MiniRBAC.Data;
public sealed class AppDbContext(DbContextOptions<AppDbContext> opt) : DbContext(opt)
{
    public DbSet<Org> Orgs => Set<Org>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<SysObject> SysObjects => Set<SysObject>();
    public DbSet<SysUserFlag> SysUserFlags => Set<SysUserFlag>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Role>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<Permission>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<RolePermission>().HasIndex(x => new { x.OrgId, x.RoleCode, x.PermissionCode }).IsUnique();
        b.Entity<UserRole>().HasIndex(x => new { x.OrgId, x.UserKey, x.RoleCode }).IsUnique();
        b.Entity<SysObject>().HasIndex(x => new { x.OrgId, x.ObjectCode }).IsUnique();
        b.Entity<SysUserFlag>().HasIndex(x => new { x.OrgId, x.UserKey }).IsUnique();
    }
}
