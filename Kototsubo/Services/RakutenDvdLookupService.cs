using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Site.Services
{
    /// <summary>楽天ブックス DVD 検索 API を使用する映像書誌検索サービス。</summary>
    public sealed partial class RakutenDvdLookupService : IDvdLookupService
    {
        private const string EndpointPath = "BooksDVD/Search/20170404";
        private readonly RakutenBooksClient _client;

        public RakutenDvdLookupService(RakutenBooksClient client)
        {
            _client = client;
        }

        public async Task<IReadOnlyList<DvdLookupResult?>> LookupByJansAsync(
            IReadOnlyList<string> jans)
        {
            var tasks = jans.Select(LookupAsync);
            return await Task.WhenAll(tasks);
        }

        internal static DvdLookupResult MapResult(JsonElement item)
        {
            var title = GetString(item, "title");
            return new DvdLookupResult
            {
                Title = title,
                Creator = GetString(item, "artistName"),
                Publisher = GetString(item, "label"),
                ReleaseDate = ParseSalesDate(GetString(item, "salesDate")),
                CoverImageUrl = GetString(item, "largeImageUrl"),
                Jan = GetString(item, "jan"),
                Format = DetectFormat(title),
                DiscCount = DetectDiscCount(title)
            };
        }

        // タイトル文字列からメディア形式を判定する。
        // 楽天BooksDVD APIにはフォーマット専用フィールドがないため
        // タイトルに含まれる表記から検出する。
        internal static string? DetectFormat(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            if (UhdBlurayPattern().IsMatch(title))
                return "4K UHD+Blu-ray";
            if (BlurayPattern().IsMatch(title))
                return "Blu-ray";
            if (DvdPattern().IsMatch(title))
                return "DVD";

            return null;
        }

        // タイトル文字列からディスク枚数を検出する（例: "2枚組", "3枚組"）。
        internal static int? DetectDiscCount(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var match = DiscCountPattern().Match(title);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                return count;

            return null;
        }

        [GeneratedRegex(@"4K\s*(?:ULTRA\s*HD|UHD)", RegexOptions.IgnoreCase)]
        private static partial Regex UhdBlurayPattern();

        [GeneratedRegex(@"Blu[\-\s]?ray|BD|ブルーレイ", RegexOptions.IgnoreCase)]
        private static partial Regex BlurayPattern();

        [GeneratedRegex(@"\bDVD\b|ディーブイディー", RegexOptions.IgnoreCase)]
        private static partial Regex DvdPattern();

        [GeneratedRegex(@"(\d+)\s*枚[組セ]")]
        private static partial Regex DiscCountPattern();

        private async Task<DvdLookupResult?> LookupAsync(string rawJan)
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
