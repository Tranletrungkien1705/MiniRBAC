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
    public DbSet<SysObjectFunction> SysObjectFunctions => Set<SysObjectFunction>();
    public DbSet<SysUserFlag> SysUserFlags => Set<SysUserFlag>();
    public DbSet<SysUserTeam> SysUserTeams => Set<SysUserTeam>();
    public DbSet<SysUserScope> SysUserScopes => Set<SysUserScope>();
    public DbSet<SysGroup> SysGroups => Set<SysGroup>();
    public DbSet<SysUserInGroup> SysUserInGroups => Set<SysUserInGroup>();
    public DbSet<SysAccess> SysAccesses => Set<SysAccess>();
    public DbSet<SysUserProfile> SysUserProfiles => Set<SysUserProfile>();
    public DbSet<SysUserInTeam> SysUserInTeams => Set<SysUserInTeam>();
    public DbSet<SysUserViewAbility> SysUserViewAbilities => Set<SysUserViewAbility>();
    public DbSet<MstDealer> MstDealers => Set<MstDealer>();
    public DbSet<MstDepartment> MstDepartments => Set<MstDepartment>();
    public DbSet<MstAreaMarket> MstAreaMarkets => Set<MstAreaMarket>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Org>().HasIndex(x => x.ApiKey).IsUnique();
        b.Entity<Role>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<Permission>().HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.Entity<RolePermission>().HasIndex(x => new { x.OrgId, x.RoleCode, x.PermissionCode }).IsUnique();
        b.Entity<UserRole>().HasIndex(x => new { x.OrgId, x.UserKey, x.RoleCode }).IsUnique();
        b.Entity<SysObject>().HasIndex(x => new { x.OrgId, x.ObjectCode }).IsUnique();
        b.Entity<SysObjectFunction>().HasIndex(x => new { x.OrgId, x.ObjectCode }).IsUnique();
        b.Entity<SysUserFlag>().HasIndex(x => new { x.OrgId, x.UserKey }).IsUnique();
        b.Entity<SysUserTeam>().HasIndex(x => new { x.OrgId, x.TeamCode, x.DealerCode }).IsUnique();
        b.Entity<SysUserScope>().HasIndex(x => new { x.OrgId, x.UserKey }).IsUnique();
        b.Entity<SysGroup>().HasIndex(x => new { x.OrgId, x.GroupCode }).IsUnique();
        b.Entity<SysUserInGroup>().HasIndex(x => new { x.OrgId, x.GroupCode, x.UserCode }).IsUnique();
        b.Entity<SysAccess>().HasIndex(x => new { x.OrgId, x.GroupCode, x.ObjectCode }).IsUnique();
        b.Entity<SysUserProfile>().HasIndex(x => new { x.OrgId, x.UserCode }).IsUnique();
        b.Entity<SysUserInTeam>().HasIndex(x => new { x.OrgId, x.UserCode, x.TeamCode, x.DealerCode }).IsUnique();
        b.Entity<SysUserViewAbility>().HasIndex(x => new { x.OrgId, x.UserCode }).IsUnique();
        b.Entity<MstDealer>().HasIndex(x => new { x.OrgId, x.DealerCode }).IsUnique();
        b.Entity<MstDepartment>().HasIndex(x => new { x.OrgId, x.DeptCode, x.DealerCode }).IsUnique();
        b.Entity<MstAreaMarket>().HasIndex(x => new { x.OrgId, x.AreaCode }).IsUnique();
    }
}
