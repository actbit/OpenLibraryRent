namespace OpenLibraryRent.Dtos;

/// <summary>
/// テナント作成結果（公開API用）
/// </summary>
public class TenantCreatePublicResultDto
{
    /// <summary>
    /// テナントID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 識別子
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// テナント名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// メッセージ
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// テナント詳細（管理用）
/// </summary>
public class TenantDetailAdminDto
{
    /// <summary>
    /// テナントID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 識別子
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// テナント名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 詳細情報
    /// </summary>
    public TenantDetailInfoDto? Detail { get; set; }
}

/// <summary>
/// テナント詳細情報
/// </summary>
public class TenantDetailInfoDto
{
    /// <summary>
    /// 貸出期間（日）
    /// </summary>
    public int? LoanPeriodDays { get; set; }

    /// <summary>
    /// ユーザーあたり最大貸出数
    /// </summary>
    public int? MaxLoansPerUser { get; set; }

    /// <summary>
    /// 延滞通知を有効にする
    /// </summary>
    public bool? EnableOverdueNotification { get; set; }

    /// <summary>
    /// メールログインを制限する
    /// </summary>
    public bool? RestrictEmailLogin { get; set; }

    /// <summary>
    /// 許可されたメールドメイン
    /// </summary>
    public string? AllowedEmailDomains { get; set; }

    /// <summary>
    /// 許可されたメールアドレス
    /// </summary>
    public string? AllowedEmails { get; set; }

    /// <summary>
    /// OIDC設定があるか
    /// </summary>
    public bool HasOidc { get; set; }

    /// <summary>
    /// OIDCオーソリティ
    /// </summary>
    public string? OpenIdConnectAuthority { get; set; }

    /// <summary>
    /// OIDCクライアントID
    /// </summary>
    public string? OpenIdConnectClientId { get; set; }

    /// <summary>
    /// クライアントシークレットがあるか
    /// </summary>
    public bool HasClientSecret { get; set; }

    /// <summary>
    /// ロールクレーム名
    /// </summary>
    public string? RoleClaimName { get; set; }
}

/// <summary>
/// テナント更新結果
/// </summary>
public class TenantUpdateResultDto
{
    /// <summary>
    /// テナントID
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 識別子
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// テナント名
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// テナント一覧アイテム
/// </summary>
public class TenantListItemDto
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public bool HasOidc { get; set; }
    public int LoanPeriodDays { get; set; }
    public int MaxLoansPerUser { get; set; }
    public int UserCount { get; set; }
    public int BookCount { get; set; }
}

/// <summary>
/// テナント詳細（識別子用）
/// </summary>
public class TenantByIdentifierDto
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public bool HasOidc { get; set; }
    public int LoanPeriodDays { get; set; }
    public int MaxLoansPerUser { get; set; }
}

/// <summary>
/// テナント作成結果
/// </summary>
public class TenantCreateResultDto
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// テナント作成制限チェック結果
/// </summary>
public class TenantLimitCheckDto
{
    public string? Email { get; set; }
    public int CurrentCount { get; set; }
    public int MaxCount { get; set; }
    public int Remaining { get; set; }
    public bool CanCreate { get; set; }
}
