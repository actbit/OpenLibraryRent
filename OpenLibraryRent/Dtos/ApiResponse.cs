namespace OpenLibraryRent.Dtos;

/// <summary>
/// 汎用APIレスポンス
/// </summary>
/// <typeparam name="T">データ型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// レスポンスデータ
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// メッセージ
    /// </summary>
    public string? Message { get; set; }

    public static ApiResponse<T> Success(T data, string? message = null)
        => new() { Data = data, Message = message };

    public static ApiResponse<T> Error(string message)
        => new() { Message = message };
}

/// <summary>
/// メッセージのみのレスポンス
/// </summary>
public class MessageResponse
{
    /// <summary>
    /// メッセージ
    /// </summary>
    public string Message { get; set; } = string.Empty;

    public MessageResponse(string message) => Message = message;
}

/// <summary>
/// バリデーションエラーレスポンス
/// </summary>
public class ValidationErrorResponse
{
    /// <summary>
    /// メッセージ
    /// </summary>
    public string Message { get; set; } = "Invalid request";

    /// <summary>
    /// フィールドごとのエラー
    /// </summary>
    public Dictionary<string, string[]> Errors { get; set; } = new();
}

/// <summary>
/// ページネーション付きリストレスポンス
/// </summary>
/// <typeparam name="T">アイテム型</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// アイテム一覧
    /// </summary>
    public List<T> Items { get; set; } = [];

    /// <summary>
    /// 総数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 現在のページ
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 1ページあたりの件数
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 総ページ数
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}
