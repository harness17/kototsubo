using System.Globalization;
using System.Text.Json;

namespace Site.Services
{
    /// <summary>楽天ブックス書籍検索 API を使用する書籍書誌検索サービス。</summary>
    public sealed class RakutenBookLookupService
    {
        private const string EndpointPath = "BooksBook/Search/20170404";
        private readonly RakutenBooksClient _client;

        public RakutenBookLookupService(RakutenBooksClient client)
        {
            _client = client;
        }

        public bool IsConfigured => _client.IsConfigured;

        public async Task<BookLookupResult?> LookupByIsbnAsync(string isbn)
        {
            var normalized = OpenBDLookupService.NormalizeIsbn13(isbn);
            if (normalized == null) return null;

            var item = await _client.SearchByIsbnAsync(EndpointPath, normalized);
            return item.HasValue ? MapResult(item.Value) : null;
        }

        internal static BookLookupResult MapResult(JsonElement item)
        {
            return new BookLookupResult
            {
                Title = GetString(item, "title"),
                Creator = GetString(item, "author"),
                Publisher = GetString(item, "publisherName"),
                ReleaseDate = ParseSalesDate(GetString(item, "salesDate")),
                CoverImageUrl = GetString(item, "largeImageUrl"),
                ISBN = OpenBDLookupService.NormalizeIsbn13(GetString(item, "isbn")),
                PageCount = null
            };
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
