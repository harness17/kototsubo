using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Site.Services
{
    /// <summary>楽天ブックス API への共通 HTTP アクセスを提供する。</summary>
    public sealed class RakutenBooksClient
    {
        private const string HttpClientName = "RakutenBooks";
        private const int MaxRetries = 2;
        private const int ThrottleDelayMs = 200;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RakutenBooksClient> _logger;
        private readonly string? _applicationId;
        private readonly string? _accessKey;
        // レート制限対策: 直列化して連続リクエストを防止
        private readonly SemaphoreSlim _requestGate = new(1, 1);

        public RakutenBooksClient(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<RakutenBooksClient>? logger = null)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger ?? NullLogger<RakutenBooksClient>.Instance;
            _applicationId = configuration["ExternalApis:RakutenApplicationId"];
            _accessKey = configuration["ExternalApis:RakutenAccessKey"];
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_applicationId);

        /// <summary>楽天ブックス書籍検索 API 用。isbn パラメータで検索する。</summary>
        public Task<JsonElement?> SearchByIsbnAsync(string endpointPath, string isbn)
            => SearchCoreAsync(endpointPath, "isbn", isbn);

        public Task<JsonElement?> SearchByJanAsync(string endpointPath, string jan)
            => SearchCoreAsync(endpointPath, "jan", jan);

        private async Task<JsonElement?> SearchCoreAsync(
            string endpointPath, string parameterName, string parameterValue)
        {
            if (!IsConfigured)
            {
                return null;
            }

            await _requestGate.WaitAsync();
            try
            {
                var requestUri =
                    $"{endpointPath}?applicationId={Uri.EscapeDataString(_applicationId!)}" +
                    (!string.IsNullOrWhiteSpace(_accessKey)
                        ? $"&accessKey={Uri.EscapeDataString(_accessKey!)}"
                        : string.Empty) +
                    $"&formatVersion=2&{parameterName}={Uri.EscapeDataString(parameterValue)}";

                for (var attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    if (attempt > 0)
                    {
                        var backoffMs = (int)Math.Pow(2, attempt) * 500;
                        _logger.LogInformation(
                            "Rakuten Books API retry {Attempt}/{MaxRetries} after {BackoffMs}ms for {ParameterValue}.",
                            attempt, MaxRetries, backoffMs, parameterValue);
                        await Task.Delay(backoffMs);
                    }

                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    HttpResponseMessage? response = null;
                    try
                    {
                        response = await client.GetAsync(requestUri);
                    }
                    catch (HttpRequestException ex) when (attempt < MaxRetries)
                    {
                        _logger.LogWarning(ex, "Rakuten Books API request failed (attempt {Attempt}).", attempt + 1);
                        continue;
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.LogWarning(ex, "Rakuten Books API request failed.");
                        return null;
                    }
                    catch (TaskCanceledException ex)
                    {
                        _logger.LogWarning(ex, "Rakuten Books API request timed out.");
                        return null;
                    }

                    using (response)
                    {
                        if (IsTransientError(response.StatusCode) && attempt < MaxRetries)
                        {
                            _logger.LogWarning(
                                "Rakuten Books API returned HTTP {StatusCode} (attempt {Attempt}), will retry.",
                                (int)response.StatusCode, attempt + 1);
                            continue;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning(
                                "Rakuten Books API returned HTTP {StatusCode} for endpoint {EndpointPath}.",
                                (int)response.StatusCode,
                                endpointPath);
                            return null;
                        }

                        try
                        {
                            await using var stream = await response.Content.ReadAsStreamAsync();
                            using var document = await JsonDocument.ParseAsync(stream);
                            if (!TryGetItems(document.RootElement, out var items) ||
                                items.ValueKind != JsonValueKind.Array ||
                                items.GetArrayLength() == 0)
                            {
                                return null;
                            }

                            return items[0].Clone();
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Rakuten Books API returned invalid JSON.");
                            return null;
                        }
                    }
                }

                return null;
            }
            finally
            {
                // 次のリクエストまで短い間隔を空ける
                _ = Task.Delay(ThrottleDelayMs).ContinueWith(_ => _requestGate.Release());
            }
        }

        private static bool IsTransientError(HttpStatusCode statusCode)
        {
            return statusCode is HttpStatusCode.TooManyRequests
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.RequestTimeout;
        }

        private static bool TryGetItems(JsonElement root, out JsonElement items)
        {
            items = default;
            return root.ValueKind == JsonValueKind.Object &&
                   (root.TryGetProperty("items", out items) ||
                    root.TryGetProperty("Items", out items));
        }
    }
}
