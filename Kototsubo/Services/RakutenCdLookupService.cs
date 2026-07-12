using System.Globalization;
using System.Text.Json;

namespace Site.Services
{
    /// <summary>楽天ブックス CD 検索 API を使用する音楽書誌検索サービス。</summary>
    public sealed class RakutenCdLookupService : ICdLookupService
    {
        private const string EndpointPath = "BooksCD/Search/20170404";
        private readonly RakutenBooksClient _client;

        public RakutenCdLookupService(RakutenBooksClient client)
        {
            _client = client;
        }

        public async Task<IReadOnlyList<CdLookupResult?>> LookupByJansAsync(
            IReadOnlyList<string> jans)
        {
            var tasks = jans.Select(LookupAsync);
            return await Task.WhenAll(tasks);
        }

        internal static CdLookupResult MapResult(JsonElement item)
        {
            return new CdLookupResult
            {
                Title = GetString(item, "title"),
                Creator = GetString(item, "artistName"),
                Publisher = GetString(item, "label"),
                ReleaseDate = ParseSalesDate(GetString(item, "salesDate")),
                CoverImageUrl = GetString(item, "largeImageUrl"),
                Jan = GetString(item, "jan"),
                DiscCount = null
            };
        }

        private async Task<CdLookupResult?> LookupAsync(string rawJan)
        {
            var jan = JanCode.Normalize(rawJan);
            if (jan == null)
            {
                return null;
            }

            var item = await _client.SearchByJanAsync(EndpointPath, jan);
            return item.HasValue ? MapResult(item.Value) : null;
        }

        private static string? GetString(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static DateTime? ParseSalesDate(string? value)
        {
            return DateTime.TryParseExact(
                value,
                "yyyy年MM月dd日",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
                ? date
                : null;
        }
    }
}
