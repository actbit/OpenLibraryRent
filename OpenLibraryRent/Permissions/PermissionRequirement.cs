namespace OpenLibraryRent.Permissions;

/// <summary>
/// 権限スコープ
/// </summary>
public enum PermissionScope
{
    /// <summary>テナントレベル権限（tenant.*）</summary>
    Tenant = 0
}

/// <summary>
/// 権限要件
/// </summary>
public sealed class PermissionRequirement : IEquatable<PermissionRequirement>
{
    public PermissionScope Scope { get; }
    public string Name { get; }

    public PermissionRequirement(PermissionScope scope, string name)
    {
        Scope = scope;
        Name = name;
    }

    public override string ToString() => Name;

    public bool Equals(PermissionRequirement? other) =>
        other is not null && Scope == other.Scope && Name == other.Name;

    public override bool Equals(object? obj) =>
        obj is PermissionRequirement other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Scope, Name);

    public static bool operator ==(PermissionRequirement? left, PermissionRequirement? right) =>
        EqualityComparer<PermissionRequirement>.Default.Equals(left, right);

    public static bool operator !=(PermissionRequirement? left, PermissionRequirement? right) =>
        !(left == right);

    /// <summary>文字列から変換</summary>
    public static PermissionRequirement Parse(string permission)
    {
        if (permission.StartsWith("tenant."))
            return new PermissionRequirement(PermissionScope.Tenant, permission);

        // デフォルトはテナントスコープ
        return new PermissionRequirement(PermissionScope.Tenant, $"tenant.{permission}");
    }
}
