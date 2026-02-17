using Finbuckle.MultiTenant;
using System.ComponentModel.DataAnnotations;

namespace OpenLibraryRent.Models;

/// <summary>
/// テナント情報
/// Finbuckle.MultiTenant用のテナント情報クラス
/// </summary>
public class ApplicationTenantInfo : TenantInfo
{
    public ApplicationTenantInfo() : base()
    {
        this.Id = Guid.CreateVersion7().ToString();
    }

    public ApplicationTenantInfo(string Id, string Identifier, string? Name = null)
    {
        this.Id = Id;
        this.Identifier = Identifier;
        this.Name = Name ?? Identifier;
    }

    public ApplicationTenantInfo(string Identifier, string? Name = null)
        : this(Guid.CreateVersion7().ToString(), Identifier, Name ?? Identifier)
    {
    }

    [StringLength(50, MinimumLength = 1, ErrorMessage = "Identifier must be between 1 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Identifier can only contain alphanumeric characters, hyphens, and underscores")]
    public new string? Identifier { get; set; }

    [StringLength(255, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 255 characters")]
    public new string? Name { get; set; }

    public ApplicationTenantDetail? Detail { get; set; }

    // Finbuckle.MultiTenant WithPerTenantAuthentication() 用のラッパープロパティ
    public string? OpenIdConnectAuthority => Detail?.OpenIdConnectAuthority;
    public string? OpenIdConnectClientId => Detail?.OpenIdConnectClientId;
    public string? OpenIdConnectClientSecret => Detail?.OpenIdConnectClientSecret;
    public string? ChallengeScheme => !string.IsNullOrEmpty(OpenIdConnectAuthority) ? "oidc" : null;
}
