namespace OpenLibraryRent.Dtos;

/// <summary>
/// ユーザー情報
/// </summary>
public class UserInfoDto
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Tenant { get; set; }
    public List<string> Roles { get; set; } = [];
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// 認証状態
/// </summary>
public class AuthStatusDto
{
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// システム認証状態
/// </summary>
public class SystemAuthStatusDto
{
    public bool IsAuthenticated { get; set; }
    public string? Email { get; set; }
}
