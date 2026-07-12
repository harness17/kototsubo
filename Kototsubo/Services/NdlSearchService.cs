using System.Net;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Site.Services
{
    public class NdlSearchService : INdlSearchService
    {
        private const string DefaultBaseUrl = "https://ndlsearch.ndl.go.jp/api/sru";
        private const int MaxAllowedRecords = 20;
        private const int MaxRetries = 1;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan RetryTimeout = TimeSpan.FromSeconds(7);
        internal const int MaxSortableRecords = 500;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NdlSearchService> _logger;
        private readonly string _baseUrl;

        private static readonly XNamespace SrwNs = "http://www.loc.gov/zing/srw/";
        private static readonly XNamespace DctermsNs = "http://purl.org/dc/terms/";
        private static readonly XNamespace RdfNs = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace FoafNs = "http://xmlns.com/foaf/0.1/";
        private static readonly XNamespace DcndlNs = "http://ndl.go.jp/dcndl/terms/";

        private const string IsbnDataType = "http://ndl.go.jp/dcndl/terms/ISBN";

        public NdlSearchService(
            IHttpClientFactory httpClientFactory,
            ILogger<NdlSearchService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = configuration["ExternalApis:NdlBaseUrl"] ?? DefaultBaseUrl;
        }

        public async Task<NdlSearchResponse> SearchAsync(
            NdlSearchCriteria criteria, int startRecord = 1, int maxRecords = 20)
        {
            maxRecords = Math.Clamp(maxRecords, 1, MaxAllowedRecords);
            startRecord = Math.Max(1, startRecord);
            var sortsLocally = criteria.SortOrder != NdlSearchSortOrder.Default;
            var apiStartRecord = sortsLocally ? 1 : startRecord;
            var apiMaxRecords = sortsLocally ? MaxSortableRecords : maxRecords;
            var query = BuildCqlQuery(criteria);
            var url = $"{_baseUrl}?operation=searchRetrieve"
                + "&version=1.2"
                + $"&query={Uri.EscapeDataString(query)}"
                + $"&startRecord={apiStartRecord}"
                + $"&maximumRecords={apiMaxRecords}"
                + "&recordSchema=dcndl";

            for (var attempt = 0; attempt <= MaxRetries; attempt++)
            {
                var stopwatch = Stopwatch.StartNew();
                using var retryTimeout = attempt == 0
                    ? null
                    : new CancellationTokenSource(RetryTimeout);
                try
                {
                    var client = _httpClientFactory.CreateClient("NdlSearch");
                    using var response = await client.GetAsync(
                        url,
                        retryTimeout?.Token ?? CancellationToken.None);
                    if (!response.IsSuccessStatusCode)
                    {
                        stopwatch.Stop();
                        if (IsTransientStatusCode(response.StatusCode) && attempt < MaxRetries)
                        {
                            _logger.LogWarning(
                                "NDL search failed with transient status {StatusCode}; retrying. ElapsedMs={ElapsedMs}.",
                                response.StatusCode,
                                stopwatch.ElapsedMilliseconds);
                            await Task.Delay(RetryDelay);
                            continue;
                        }

                        _logger.LogWarning(
                            "NDL search failed with status {StatusCode}. ElapsedMs={ElapsedMs}.",
                            response.StatusCode,
                            stopwatch.ElapsedMilliseconds);
                        return FailedResponse();
                    }

                    await using var stream = await response.Content.ReadAsStreamAsync(
                        retryTimeout?.Token ?? CancellationToken.None);
                    var doc = await XDocument.LoadAsync(
                        stream,
                        LoadOptions.None,
                        retryTimeout?.Token ?? CancellationToken.None);
                    var result = ParseResponse(doc);
                    if (!result.Succeeded)
                    {
                        stopwatch.Stop();
                        _logger.LogWarning(
                            "NDL search response was structurally invalid. ElapsedMs={ElapsedMs}.",
                            stopwatch.ElapsedMilliseconds);
                        return result;
                    }

                    return sortsLocally
                        ? SortAndPageResults(result, criteria.SortOrder, startRecord, maxRecords)
                        : result;
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < MaxRetries)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(
                        ex,
                        "NDL search failed with transient exception; retrying. ElapsedMs={ElapsedMs}.",
                        stopwatch.ElapsedMilliseconds);
                    await Task.Delay(RetryDelay);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(
                        ex,
                        "NDL search failed for query: {Query}. ElapsedMs={ElapsedMs}",
                        query,
                        stopwatch.ElapsedMilliseconds);
                    return FailedResponse();
                }
            }

            // ループ内の全分岐は実行時に return するが、コンパイラの終端到達性チェックのために必要。
            return FailedResponse();
        }

        private static NdlSearchResponse FailedResponse() => new() { Succeeded = false };

        private static bool IsTransientException(Exception ex)
            => ex is TaskCanceledException or HttpRequestException or IOException;

        private static bool IsTransientStatusCode(HttpStatusCode statusCode)
            => statusCode is
                HttpStatusCode.RequestTimeout or
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.InternalServerError or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable or
                HttpStatusCode.GatewayTimeout;

        internal static string BuildCqlQuery(NdlSearchCriteria criteria)
        {
            // 指定された項目のみを AND 結合する。最後に図書館目録レコードへ絞る dpid を付与。
            var clauses = new List<string>();

            if (!string.IsNullOrWhiteSpace(criteria.ISBN))
                clauses.Add($"isbn=\"{Sanitize(criteria.ISBN)}\"");
            if (!string.IsNullOrWhiteSpace(criteria.Title))
                clauses.Add($"title=\"{Sanitize(criteria.Title)}\"");
            if (!string.IsNullOrWhiteSpace(criteria.Creator))
                clauses.Add($"creator=\"{Sanitize(criteria.Creator)}\"");
            if (!string.IsNullOrWhiteSpace(criteria.Publisher))
                clauses.Add($"publisher=\"{Sanitize(criteria.Publisher)}\"");
            if (criteria.YearFrom.HasValue)
                clauses.Add($"from=\"{criteria.YearFrom.Value}\"");
            if (criteria.YearTo.HasValue)
                clauses.Add($"until=\"{criteria.YearTo.Value}\"");

            // ISBN 検索に dpid を併用すると NDL SRU が診断エラーを返すため、
            // ISBN 指定時は全データプロバイダから完全一致するレコードを選ぶ。
            if (string.IsNullOrWhiteSpace(criteria.ISBN))
                clauses.Add("dpid=\"iss-ndl-opac\"");
            return string.Join(" AND ", clauses);
        }

        internal static NdlSearchResponse SortAndPageResults(
            NdlSearchResponse response,
            NdlSearchSortOrder sortOrder,
            int startRecord,
            int maxRecords)
        {
            IEnumerable<NdlSearchResult> sorted = sortOrder switch
            {
                NdlSearchSortOrder.PublicationDateDescending =>
                    response.Results
                        .OrderByDescending(x => TryGetPublicationYear(x.PublicationDate).HasValue)
                        .ThenByDescending(x => TryGetPublicationYear(x.PublicationDate)),
                NdlSearchSortOrder.PublicationDateAscending =>
                    response.Results
                        .OrderByDescending(x => TryGetPublicationYear(x.PublicationDate).HasValue)
                        .ThenBy(x => TryGetPublicationYear(x.PublicationDate)),
                _ => response.Results
            };

            return new NdlSearchResponse
            {
                Succeeded = response.Succeeded,
                TotalResults = Math.Min(response.TotalResults, MaxSortableRecords),
                IsTruncated = response.TotalResults > MaxSortableRecords,
                Results = sorted
                    .Skip(Math.Max(0, startRecord - 1))
                    .Take(Math.Clamp(maxRecords, 1, MaxAllowedRecords))
                    .ToList()
            };
        }

        private static int? TryGetPublicationYear(string? publicationDate)
        {
            if (string.IsNullOrWhiteSpace(publicationDate))
                return null;

            var match = Regex.Match(publicationDate, @"(?<!\d)(\d{4})(?!\d)");
            return match.Success && int.TryParse(match.Groups[1].Value, out var year)
                ? year
                : null;
        }

        /// <summary>CQL の二重引用符による構文崩れを防ぐため、入力値から引用符を除去する。</summary>
        private static string Sanitize(string value) => value.Replace("\"", "");

        internal NdlSearchResponse ParseResponse(XDocument doc)
        {
            var root = doc.Root;
            if (root == null) return FailedResponse();

            var result = new NdlSearchResponse();

            var numberOfRecords = root.Element(SrwNs + "numberOfRecords");
            if (numberOfRecords != null &&
                int.TryParse(numberOfRecords.Value, out var total))
            {
                result.TotalResults = total;
            }

            var records = root
                .Element(SrwNs + "records")?
                .Elements(SrwNs + "record") ?? [];

            foreach (var record in records)
            {
                var recordData = record.Element(SrwNs + "recordData");
                if (recordData == null) continue;

                // NDL は recordPacking="string" で返すことがあり、その場合 recordData の
                // 中身は XML エスケープされた文字列（&lt;rdf:RDF...&gt;）になる。
                // 子要素を持たない（テキストのみ）なら、その文字列を XML として再パースする。
                var container = recordData.HasElements
                    ? recordData
                    : ParseEscapedRecordData(recordData.Value);
                if (container == null) continue;

                var bibResource = container.Descendants(DcndlNs + "BibResource").FirstOrDefault()
                    ?? container.Descendants().FirstOrDefault();
                if (bibResource == null) continue;

                var item = new NdlSearchResult
                {
                    Title = GetElementValue(bibResource, DctermsNs + "title"),
                    Creator = GetAgentName(bibResource, DctermsNs + "creator"),
                    Publisher = GetAgentName(bibResource, DctermsNs + "publisher"),
                    ISBN = GetIsbn(bibResource),
                    PublicationDate = GetElementValue(bibResource, DctermsNs + "issued")
                };

                result.Results.Add(item);
            }

            return result;
        }

        // recordPacking="string" の recordData をデコードして XML として再パースする。
        // パース失敗時は null を返し、その record をスキップする。
        private XElement? ParseEscapedRecordData(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                return XElement.Parse(text);
            }
            catch (System.Xml.XmlException ex)
            {
                _logger.LogWarning(ex, "NDL recordData の XML パースに失敗しました。");
                return null;
            }
        }

        private static string? GetElementValue(XElement parent, XName name)
        {
            var element = parent.Element(name);
            if (element == null) return null;
            var value = element.Value.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static string? GetAgentName(XElement parent, XName name)
        {
            var element = parent.Element(name);
            if (element == null) return null;

            var agent = element.Element(FoafNs + "Agent");
            if (agent != null)
            {
                var foafName = agent.Element(FoafNs + "name");
                if (foafName != null)
                {
                    var value = foafName.Value.Trim();
                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }

            var text = element.Value.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static string? GetIsbn(XElement parent)
        {
            foreach (var id in parent.Elements(DctermsNs + "identifier"))
            {
                var dataType = id.Attribute(RdfNs + "datatype")?.Value;
                if (string.Equals(dataType, IsbnDataType, StringComparison.OrdinalIgnoreCase))
                {
                    var value = id.Value.Trim();
                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }
            return null;
        }
    }
}
