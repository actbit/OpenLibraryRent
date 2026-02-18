using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// テナント詳細設定
/// OIDC設定、貸出設定などを管理
/// </summary>
public class ApplicationTenantDetail
{
    [Key]
    public string TenantId { get; set; } = null!;

    public ApplicationTenantInfo? Tenant { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "MetadataAddress must be a valid URL")]
    public string? OpenIdConnectMetadataAddress { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "Authority must be a valid URL")]
    public string? OpenIdConnectAuthority { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "AuthorizationEndpoint must be a valid URL")]
    public string? OpenIdConnectAuthorizationEndpoint { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "TokenEndpoint must be a valid URL")]
    public string? OpenIdConnectTokenEndpoint { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "JwksUri must be a valid URL")]
    public string? OpenIdConnectJwksUri { get; set; }

    [StringLength(500)]
    [Url(ErrorMessage = "EndSessionEndpoint must be a valid URL")]
    public string? OpenIdConnectEndSessionEndpoint { get; set; }

    [StringLength(500, MinimumLength = 1, ErrorMessage = "ClientId must be between 1 and 500 characters")]
    public string? OpenIdConnectClientId { get; set; }

    [StringLength(1000)]
    public string? OpenIdConnectClientSecret { get; set; }

    /// <summary>
    /// ClientSecret暗号化用のテナントキー（Base64エンコード）
    /// </summary>
    [StringLength(200)]
    public string? TenantEncryptionKey { get; set; }

    public string? RoleClaimName { get; set; }

    /// <summary>
    /// 貸出期間（日数）
    /// </summary>
    public int LoanPeriodDays { get; set; } = 14;

    /// <summary>
    /// 最大貸出冊数（ユーザーあたり）
    /// </summary>
    public int MaxLoansPerUser { get; set; } = 5;

    /// <summary>
    /// 延滞通知を有効にするかどうか
    /// </summary>
    public bool EnableOverdueNotification { get; set; } = true;

    /// <summary>
    /// メールアドレスによるログイン制限を有効にするかどうか
    /// </summary>
    public bool RestrictEmailLogin { get; set; } = false;

    /// <summary>
    /// 許可するメールドメイン（カンマ区切り、例: "company.com,example.org"）
    /// </summary>
    [StringLength(1000)]
    public string? AllowedEmailDomains { get; set; }

    /// <summary>
    /// 許可するメールアドレス（カンマ区切り）
    /// </summary>
    [StringLength(2000)]
    public string? AllowedEmails { get; set; }
}

public static class ApplicationTenantDetailExtensions
{
    /// <summary>
    /// OIDC設定が有効かどうかを判定
    /// </summary>
    public static bool HasOidcSettings(this ApplicationTenantDetail? detail)
    {
        if (detail == null) return false;

        var hasMetadataOrAuthority =
            !string.IsNullOrWhiteSpace(detail.OpenIdConnectMetadataAddress) ||
            !string.IsNullOrWhiteSpace(detail.OpenIdConnectAuthority);

        var hasClientId = !string.IsNullOrWhiteSpace(detail.OpenIdConnectClientId);

        return hasMetadataOrAuthority && hasClientId;
    }

    /// <summary>
    /// OIDCロール同期が有効かどうかを判定
    /// </summary>
    public static bool HasOidcRoleSync(this ApplicationTenantDetail? detail)
    {
        if (!detail.HasOidcSettings()) return false;
        return !string.IsNullOrWhiteSpace(detail.RoleClaimName);
    }

    /// <summary>
    /// ユーザー作成が許可されているかどうかを判定
    /// </summary>
    public static bool CanCreateUsers(this ApplicationTenantDetail? detail)
    {
        return !detail.HasOidcSettings();
    }

    /// <summary>
    /// ユーザーへのロール割り当てが許可されているかどうかを判定
    /// </summary>
    public static bool CanAssignRolesToUsers(this ApplicationTenantDetail? detail)
    {
        return !detail.HasOidcRoleSync();
    }

    /// <summary>
    /// 指定されたメールアドレスがログイン許可されているかどうかを判定
    /// </summary>
    public static bool IsEmailAllowed(this ApplicationTenantDetail? detail, string? email)
    {
        if (detail == null || !detail.RestrictEmailLogin)
        {
            // 制限が無効な場合は全て許可
            return true;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // 許可されたメールアドレスリストをチェック
        if (!string.IsNullOrWhiteSpace(detail.AllowedEmails))
        {
            var allowedEmails = detail.AllowedEmails
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(e => e.ToLowerInvariant());

            if (allowedEmails.Contains(normalizedEmail))
            {
                return true;
            }
        }

        // 許可されたドメインリストをチェック
        if (!string.IsNullOrWhiteSpace(detail.AllowedEmailDomains))
        {
            var allowedDomains = detail.AllowedEmailDomains
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => d.ToLowerInvariant().TrimStart('@'));

            var emailDomain = normalizedEmail.Split('@').LastOrDefault();
            if (!string.IsNullOrEmpty(emailDomain) && allowedDomains.Contains(emailDomain))
            {
                return true;
            }
        }

        return false;
    }
}
