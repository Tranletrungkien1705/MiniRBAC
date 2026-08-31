namespace MiniRBAC.Data;
public interface ITenantContext { Guid OrgId { get; set; } }
public sealed class TenantContext : ITenantContext
{
    public static readonly Guid DefaultOrgId = new("77777777-7777-7777-7777-777777777777");
    public const string CookieName = "org_key";
    public Guid OrgId { get; set; } = DefaultOrgId;
}
