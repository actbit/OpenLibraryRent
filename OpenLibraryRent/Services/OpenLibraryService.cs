using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenLibraryRent.Services.Caching;

namespace OpenLibraryRent.Services;

/// <summary>
/// Open Library API クライアント
/// ISBNから書籍情報を取得（キャッシュ対応）
/// </summary>
public class OpenLibraryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryService> _logger;
    private readonly ICacheService _cache;

    private const string BaseUrl = "https://openlibrary.org";

    /// <summary>
    /// 書籍情報のキャッシュ有効期限（24時間）
    /// </summary>
    private static readonly TimeSpan BookCacheExpiration = TimeSpan.FromHours(24);

    public OpenLibraryService(
        HttpClient httpClient,
        ILogger<OpenLibraryService> logger,
        ICacheService cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// ISBNで書籍情報を取得（キャッシュ付き）
    /// </summary>
    public async Task<OpenLibraryBookResponse?> GetBookByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        // ISBNからハイフンとスペースを削除
        var cleanIsbn = isbn.Replace("-", "").Replace(" ", "");

        // キャッシュから取得を試みる
        var cacheKey = CacheKeys.BookByIsbn(cleanIsbn);
        var cached = await _cache.GetAsync<OpenLibraryBookResponse>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Book cache hit: {Isbn}", cleanIsbn);
            return cached;
        }

        _logger.LogDebug("Book cache miss: {Isbn}", cleanIsbn);

        try
        {
            var url = $"{BaseUrl}/isbn/{cleanIsbn}.json";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Book not found for ISBN: {Isbn}", cleanIsbn);
                    return null;
                }

                _logger.LogWarning("Failed to fetch book for ISBN {Isbn}: {StatusCode}", cleanIsbn, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var bookData = JsonSerializer.Deserialize<OpenLibraryBookData>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bookData == null)
            {
                return null;
            }

            // 著者情報を取得
            var authors = await GetAuthorsAsync(bookData.Authors, cancellationToken);

            // 表紙画像URLを構築
            string? coverUrl = null;
            if (bookData.CoverId.HasValue)
            {
                coverUrl = $"https://covers.openlibrary.org/b/id/{bookData.CoverId}-L.jpg";
            }
            else if (bookData.Covers != null && bookData.Covers.Length > 0)
            {
                coverUrl = $"https://covers.openlibrary.org/b/id/{bookData.Covers[0]}-L.jpg";
            }

            // 出版年を抽出
            int? publishYear = null;
            if (!string.IsNullOrEmpty(bookData.PublishDate))
            {
                // 年のみを抽出（例: "2020", "2020-01-01", "January 2020"）
                var yearMatch = System.Text.RegularExpressions.Regex.Match(bookData.PublishDate, @"\b(19|20)\d{2}\b");
                if (yearMatch.Success && int.TryParse(yearMatch.Value, out var year))
                {
                    publishYear = year;
                }
            }

            var result = new OpenLibraryBookResponse
            {
                Isbn = cleanIsbn,
                Title = bookData.Title,
                Authors = string.Join(", ", authors),
                Publisher = bookData.Publishers?.FirstOrDefault(),
                PublishYear = publishYear,
                PageCount = bookData.NumberOfPages,
                CoverImageUrl = coverUrl,
                Description = bookData.Description?.Value ?? bookData.Description?.ToString()
            };

            // キャッシュに保存
            await _cache.SetAsync(cacheKey, result, BookCacheExpiration, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching book for ISBN: {Isbn}", cleanIsbn);
            return null;
        }
    }

    private async Task<List<string>> GetAuthorsAsync(string[]? authorKeys, CancellationToken cancellationToken)
    {
        var authors = new List<string>();

        if (authorKeys == null || authorKeys.Length == 0)
        {
            return authors;
        }

        foreach (var authorKey in authorKeys.Take(5)) // 最大5名まで
        {
            try
            {
                // authorKeyは "/authors/OLxxx" 形式
                var url = $"{BaseUrl}{authorKey}.json";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    var authorData = JsonSerializer.Deserialize<OpenLibraryAuthorData>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (!string.IsNullOrEmpty(authorData?.Name))
                    {
                        authors.Add(authorData.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch author: {AuthorKey}", authorKey);
            }
        }

        return authors;
    }

    /// <summary>
    /// ISBN検索（ISBN 10/13両対応）
    /// </summary>
    public async Task<OpenLibraryBookResponse?> SearchByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        return await GetBookByIsbnAsync(isbn, cancellationToken);
    }
}

/// <summary>
/// Open Library API レスポンス（変換済み）
/// </summary>
public class OpenLibraryBookResponse
{
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Authors { get; set; }
    public string? Publisher { get; set; }
    public int? PublishYear { get; set; }
    public int? PageCount { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Open Library API 書籍データ（生データ）
/// </summary>
internal class OpenLibraryBookData
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("authors")]
    public string[]? Authors { get; set; }

    [JsonPropertyName("publishers")]
    public string[]? Publishers { get; set; }

    [JsonPropertyName("publish_date")]
    public string? PublishDate { get; set; }

    [JsonPropertyName("number_of_pages")]
    public int? NumberOfPages { get; set; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; set; }

    [JsonPropertyName("covers")]
    public int[]? Covers { get; set; }

    [JsonPropertyName("description")]
    public dynamic? Description { get; set; }
}

/// <summary>
/// Open Library API 著者データ
/// </summary>
internal class OpenLibraryAuthorData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
