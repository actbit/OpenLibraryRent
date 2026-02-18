namespace OpenLibraryRent.Permissions;

/// <summary>
/// テナントレベルの権限
/// </summary>
public static class TenantPermissions
{
    // テナント設定
    public const string TenantRead = "tenant.read";
    public static readonly PermissionRequirement TenantReadReq = new(PermissionScope.Tenant, TenantRead);

    public const string TenantManage = "tenant.manage";
    public static readonly PermissionRequirement TenantManageReq = new(PermissionScope.Tenant, TenantManage);

    // ユーザー管理
    public const string UserRead = "tenant.user.read";
    public static readonly PermissionRequirement UserReadReq = new(PermissionScope.Tenant, UserRead);

    public const string UserManage = "tenant.user.manage";
    public static readonly PermissionRequirement UserManageReq = new(PermissionScope.Tenant, UserManage);

    // ロール管理
    public const string RoleRead = "tenant.role.read";
    public static readonly PermissionRequirement RoleReadReq = new(PermissionScope.Tenant, RoleRead);

    public const string RoleManage = "tenant.role.manage";
    public static readonly PermissionRequirement RoleManageReq = new(PermissionScope.Tenant, RoleManage);

    // 書籍管理
    public const string BookRead = "tenant.book.read";
    public static readonly PermissionRequirement BookReadReq = new(PermissionScope.Tenant, BookRead);

    public const string BookManage = "tenant.book.manage";
    public static readonly PermissionRequirement BookManageReq = new(PermissionScope.Tenant, BookManage);

    // 貸出管理
    public const string RentalRead = "tenant.rental.read";
    public static readonly PermissionRequirement RentalReadReq = new(PermissionScope.Tenant, RentalRead);

    public const string RentalManage = "tenant.rental.manage";
    public static readonly PermissionRequirement RentalManageReq = new(PermissionScope.Tenant, RentalManage);

    // 延滞管理
    public const string OverdueRead = "tenant.overdue.read";
    public static readonly PermissionRequirement OverdueReadReq = new(PermissionScope.Tenant, OverdueRead);

    public const string OverdueManage = "tenant.overdue.manage";
    public static readonly PermissionRequirement OverdueManageReq = new(PermissionScope.Tenant, OverdueManage);
}
