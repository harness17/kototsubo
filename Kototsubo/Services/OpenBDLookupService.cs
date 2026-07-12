using System.Globalization;
using System.Text.Json;

namespace Site.Services
{
    public class OpenBDLookupService : IBookLookupService
    {
        private const string DefaultBaseUrl = "https://api.openbd.jp/v1/get";
        internal const int BatchSize = 100;
        private const int MaxConcurrentRequests = 4;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenBDLookupService> _logger;
        private readonly string _baseUrl;

        public OpenBDLookupService(
            IHttpClientFactory httpClientFactory,
            ILogger<OpenBDLookupService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = configuration["ExternalApis:OpenBDBaseUrl"] ?? DefaultBaseUrl;
        }

        public async Task<BookLookupResult?> LookupByIsbnAsync(string isbn)
        {
            var results = await LookupByIsbnsAsync([isbn]);
            return results[0];
        }

        public async Task<IReadOnlyList<BookLookupResult?>> LookupByIsbnsAsync(
            IReadOnlyList<string> isbns)
        {
            var results = new BookLookupResult?[isbns.Count];
            var validRequests = isbns
                .Select((isbn, index) => new
                {
                    Index = index,
                    ISBN = NormalizeIsbn13(isbn)
                })
                .Where(x => x.ISBN != null)
                .Select(x => new LookupRequest(x.Index, x.ISBN!))
                .ToList();

            if (validRequests.Count == 0) return results;

            using var semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            var tasks = validRequests
                .Chunk(BatchSize)
                .Select(async batch =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await LookupBatchAsync(batch, results);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            await Task.WhenAll(tasks);

            return results;
        }

        /// <summary>ASINからAmazon書影URLを生成する（images-fe = 日本向け）。</summary>
        internal static string GetAmazonCoverUrl(string asin)
            => $"https://images-fe.ssl-images-amazon.com/images/P/{asin}.09.LZZZZZZZ.jpg";

        internal static string? ToAmazonAsinCandidate(string? isbn)
        {
            var isbn13 = NormalizeIsbn13(isbn);
            if (isbn13 == null || !isbn13.StartsWith("978", StringComparison.Ordinal))
                return null;

            var body = isbn13.Substring(3, 9);
            var sum = body
                .Select((c, index) => (c - '0') * (10 - index))
                .Sum();
            var checkDigit = (11 - sum % 11) % 11;
            return body + (checkDigit == 10
                ? "X"
                : checkDigit.ToString(CultureInfo.InvariantCulture));
        }

        private async Task LookupBatchAsync(
            IEnumerable<LookupRequest> batch,
            BookLookupResult?[] results)
        {
            var requests = batch.ToList();

            try
            {
                var client = _httpClientFactory.CreateClient("OpenBD");
                using var response = await client.GetAsync(
                    $"{_baseUrl}?isbn={string.Join(",", requests.Select(x => x.ISBN))}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "openBD lookup failed with status code {StatusCode}.",
                        response.StatusCode);
                    return;
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                var books = document.RootElement;
                for (var i = 0; i < requests.Count && i < books.GetArrayLength(); i++)
                {
                    var book = books[i];
                    if (book.ValueKind == JsonValueKind.Null ||
                        !TryGetPropertyIgnoreCase(book, "summary", out var summary))
                    {
                        continue;
                    }

                    var responseIsbn = NormalizeIsbn13(GetString(summary, "isbn"));
                    results[requests[i].Index] = new BookLookupResult
                    {
                        Title = GetString(summary, "title"),
                        Creator = GetString(summary, "author"),
                        Publisher = GetString(summary, "publisher"),
                        ReleaseDate = ParsePublicationDate(GetString(summary, "pubdate")),
                        CoverImageUrl = GetString(summary, "cover"),
                        ISBN = responseIsbn ?? requests[i].ISBN,
                        PageCount = GetPageCount(book)
                    };
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "openBD lookup failed.");
            }
        }

        private sealed record LookupRequest(int Index, string ISBN);

        internal static string? NormalizeIsbn13(string? isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn)) return null;

            var compact = new string(isbn
                .Select(NormalizeFullWidth)
                .Where(c => c != '-' && !char.IsWhiteSpace(c))
                .Select(char.ToUpperInvariant)
                .ToArray());

            if (compact.Length == 13 && compact.All(char.IsDigit))
                return HasValidIsbn13CheckDigit(compact) ? compact : null;

            if (compact.Length != 10 ||
                !compact[..9].All(char.IsDigit) ||
                !HasValidIsbn10CheckDigit(compact))
            {
                return null;
            }

            var prefix = "978" + compact[..9];
            var weightedSum = prefix
                .Select((c, index) => (c - '0') * (index % 2 == 0 ? 1 : 3))
                .Sum();
            var checkDigit = (10 - weightedSum % 10) % 10;
            return prefix + checkDigit.ToString(CultureInfo.InvariantCulture);
        }

        private static bool HasValidIsbn13CheckDigit(string isbn)
        {
            var sum = isbn
                .Take(12)
                .Select((c, index) => (c - '0') * (index % 2 == 0 ? 1 : 3))
                .Sum();
            return (10 - sum % 10) % 10 == isbn[12] - '0';
        }

        private static bool HasValidIsbn10CheckDigit(string isbn)
        {
            var sum = isbn
                .Take(9)
                .Select((c, index) => (c - '0') * (10 - index))
                .Sum();
            var expected = (11 - sum % 11) % 11;
            var actual = isbn[9] == 'X' ? 10 : isbn[9] - '0';
            return expected == actual;
        }

        // Full-width ASCII (U+FF01–U+FF5E) → half-width (U+0021–U+007E)
        private static char NormalizeFullWidth(char c)
        {
            if (c is >= '！' and <= '～')
                return (char)(c - 0xFEE0);
            if (c == '　') // full-width space
                return ' ';
            return c;
        }

        internal static DateTime? ParsePublicationDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return null;

            if (!int.TryParse(digits[..4], out var year) || year is < 1 or > 9999)
                return null;

            var month = 1;
            var day = 1;
            if (digits.Length >= 6 &&
                int.TryParse(digits.Substring(4, 2), out var parsedMonth) &&
                parsedMonth is >= 1 and <= 12)
            {
                month = parsedMonth;
            }

            if (digits.Length >= 8 &&
                int.TryParse(digits.Substring(6, 2), out var parsedDay) &&
                parsedDay >= 1 &&
                parsedDay <= DateTime.DaysInMonth(year, month))
            {
                day = parsedDay;
            }

            return new DateTime(year, month, day);
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return TryGetPropertyIgnoreCase(element, propertyName, out var property) &&
                   property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static int? GetPageCount(JsonElement book)
        {
            if (!TryGetPropertyIgnoreCase(book, "onix", out var onix) ||
                !TryGetPropertyIgnoreCase(onix, "DescriptiveDetail", out var detail) ||
                !TryGetPropertyIgnoreCase(detail, "Extent", out var extents) ||
                extents.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var extent in extents.EnumerateArray())
            {
                if (!TryGetPropertyIgnoreCase(extent, "ExtentValue", out var value))
                    continue;

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                    return numeric;

                if (value.ValueKind == JsonValueKind.String &&
                    int.TryParse(value.GetString(), out var textValue))
                {
                    return textValue;
                }
            }

            return null;
        }

        private static bool TryGetPropertyIgnoreCase(
            JsonElement element,
            string propertyName,
            out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }
    }
}
