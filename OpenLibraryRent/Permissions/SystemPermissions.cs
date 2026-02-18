namespace OpenLibraryRent.Permissions;

/// <summary>
/// システムレベルの権限（システム管理テナントのみ）
/// </summary>
public static class SystemPermissions
{
    // システム管理テナント識別子
    public const string SystemTenantIdentifier = "system";

    // テナント管理
    public const string TenantCreate = "system.tenant.create";
    public static readonly PermissionRequirement TenantCreateReq = new(PermissionScope.Tenant, TenantCreate);

    public const string TenantRead = "system.tenant.read";
    public static readonly PermissionRequirement TenantReadReq = new(PermissionScope.Tenant, TenantRead);

    public const string TenantManage = "system.tenant.manage";
    public static readonly PermissionRequirement TenantManageReq = new(PermissionScope.Tenant, TenantManage);

    public const string TenantDelete = "system.tenant.delete";
    public static readonly PermissionRequirement TenantDeleteReq = new(PermissionScope.Tenant, TenantDelete);

    // システム設定
    public const string SettingsRead = "system.settings.read";
    public static readonly PermissionRequirement SettingsReadReq = new(PermissionScope.Tenant, SettingsRead);

    public const string SettingsManage = "system.settings.manage";
    public static readonly PermissionRequirement SettingsManageReq = new(PermissionScope.Tenant, SettingsManage);
}
