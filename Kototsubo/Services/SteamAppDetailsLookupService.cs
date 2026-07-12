using System.Text.Json;

namespace Site.Services
{
    /// <summary>
    /// Steam Store の非公式 appdetails API（store.steampowered.com/api/appdetails）を使用する。
    /// 2023年後半以降、Steam はカバー画像のパスをアプリごとの動的ハッシュ付きURLへ移行しており、
    /// 固定パス（/steam/apps/{appid}/header.jpg）は新しいアプリでは404になるため、
    /// このAPIから都度 header_image を取得する必要がある。
    /// 非公式APIでレート制限が厳しいため、失敗時は例外を投げず null にフォールバックする。
    /// </summary>
    public sealed class SteamAppDetailsLookupService : ISteamAppDetailsLookupService
    {
        private const string HttpClientName = "SteamStore";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SteamAppDetailsLookupService> _logger;

        public SteamAppDetailsLookupService(
            IHttpClientFactory httpClientFactory,
            ILogger<SteamAppDetailsLookupService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> GetHeaderImageUrlAsync(string appId)
        {
            string? headerImageUrl = null;
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync(
                    $"api/appdetails?appids={Uri.EscapeDataString(appId)}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Steam appdetails API returned HTTP {StatusCode} for AppId {AppId}.",
                        (int)response.StatusCode, appId);
                    return await FindStaticCoverImageUrlAsync(appId);
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                headerImageUrl = ExtractHeaderImage(document.RootElement, appId);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Steam appdetails API request failed for AppId {AppId}.", appId);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Steam appdetails API request timed out for AppId {AppId}.", appId);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Steam appdetails API returned invalid JSON for AppId {AppId}.", appId);
            }

            return headerImageUrl ?? await FindStaticCoverImageUrlAsync(appId);
        }

        internal static string? ExtractHeaderImage(JsonElement root, string appId)
        {
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(appId, out var entry) ||
                entry.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!entry.TryGetProperty("success", out var success) ||
                success.ValueKind != JsonValueKind.True)
            {
                return null;
            }

            if (!entry.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("header_image", out var headerImage) ||
                headerImage.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var url = headerImage.GetString();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }

        internal static IReadOnlyList<string> BuildStaticCoverImageCandidates(string appId)
        {
            if (!IsValidAppIdFormat(appId)) return Array.Empty<string>();

            var encodedAppId = Uri.EscapeDataString(appId);
            var assets = new[]
            {
                "library_hero.jpg",
                "header.jpg",
                "capsule_616x353.jpg",
                "capsule_467x181.jpg",
                "library_600x900.jpg"
            };

            return assets
                .Select(asset => $"https://cdn.akamai.steamstatic.com/steam/apps/{encodedAppId}/{asset}")
                .ToList();
        }

        private async Task<string?> FindStaticCoverImageUrlAsync(string appId)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            foreach (var url in BuildStaticCoverImageCandidates(appId))
            {
                try
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    if (response.IsSuccessStatusCode &&
                        response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return url;
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Steam static cover image lookup failed for URL {Url}.", url);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(ex, "Steam static cover image lookup timed out for URL {Url}.", url);
                }
            }

            return null;
        }

        private static bool IsValidAppIdFormat(string appId)
        {
            return !string.IsNullOrWhiteSpace(appId) &&
                   appId.Length <= 20 &&
                   appId.All(char.IsDigit);
        }
    }
}
