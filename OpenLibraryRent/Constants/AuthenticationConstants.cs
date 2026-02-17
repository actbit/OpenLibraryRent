namespace OpenLibraryRent.Constants;

/// <summary>
/// 認証関連の定数をまとめたクラス
/// </summary>
public static class AuthenticationConstants
{
    /// <summary>
    /// テナント識別用の Claim タイプ
    /// </summary>
    public const string TenantClaimType = "tenant";

    /// <summary>
    /// OIDC スキーム名
    /// </summary>
    public const string OidcSchemeName = "oidc";

    /// <summary>
    /// Cookie 認証スキーム名
    /// </summary>
    public const string CookieSchemeName = "Cookies";

    /// <summary>
    /// Cookie 認証のデフォルトスキーム名
    /// </summary>
    public const string DefaultAuthenticationScheme = "Cookies";

    /// <summary>
    /// 認証パス関連
    /// </summary>
    public static class Paths
    {
        public const string LoginPath = "/auth/login";
        public const string LogoutPath = "/auth/logout";
        public const string OidcCallbackPath = "/auth/signin-oidc";
        public const string ApiPrefix = "/api";

        public static bool IsApiPath(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            var trimmed = path.Trim('/');
            if (trimmed.Length == 0)
                return false;

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length >= 2 && string.Equals(segments[1], "api", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 静的ファイル検出用パターン
    /// </summary>
    public static class StaticFilePaths
    {
        public const string SvelteKitAssets = "/_app";
        public const string CssDirectory = "/css";
        public const string JavaScriptDirectory = "/js";
        public const string ImagesDirectory = "/img";
        public const string FontsDirectory = "/fonts";
        public const string CssFileExtension = ".css";
        public const string JavaScriptFileExtension = ".js";
        public const string PngFileExtension = ".png";
        public const string JpegFileExtension = ".jpg";
        public const string IcoFileExtension = ".ico";
        public const string WoffFileExtension = ".woff";
        public const string Woff2FileExtension = ".woff2";

        public static bool IsStaticFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith(SvelteKitAssets) ||
                path.StartsWith(CssDirectory) ||
                path.StartsWith(JavaScriptDirectory) ||
                path.StartsWith(ImagesDirectory) ||
                path.StartsWith(FontsDirectory))
                return true;

            if (path.EndsWith(CssFileExtension) ||
                path.EndsWith(JavaScriptFileExtension) ||
                path.EndsWith(PngFileExtension) ||
                path.EndsWith(JpegFileExtension) ||
                path.EndsWith(IcoFileExtension) ||
                path.EndsWith(WoffFileExtension) ||
                path.EndsWith(Woff2FileExtension))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Cookie 関連の定数
    /// </summary>
    public static class Cookie
    {
        public const int ExpirationHours = 8;
        public const string CookiePath = "/";
        public const string TenantCookieNameSeparator = "_";
        public const string TenantCookieSuffix = ".Tenant";
        public const string TenantForCookieKey = "tenant_for_cookie";
    }

    /// <summary>
    /// CORS ポリシー名
    /// </summary>
    public static class CorsPolicy
    {
        public const string Development = "development";
        public const string Production = "production";
    }
}
