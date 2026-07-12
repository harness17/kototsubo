namespace Site.Services
{
    /// <summary>
    /// 国立国会図書館サーチの書誌を優先し、openBD で付加情報と欠損項目を補完する。
    /// </summary>
    public class NdlPrimaryBookLookupService :
        IBookLookupService,
        IBookCandidateLookupService
    {
        private const int MaxConcurrentNdlRequests = 4;
        private readonly OpenBDLookupService _openBd;
        private readonly INdlSearchService _ndlSearch;
        private readonly ILogger<NdlPrimaryBookLookupService> _logger;

        public NdlPrimaryBookLookupService(
            OpenBDLookupService openBd,
            INdlSearchService ndlSearch,
            ILogger<NdlPrimaryBookLookupService> logger)
        {
            _openBd = openBd;
            _ndlSearch = ndlSearch;
            _logger = logger;
        }

        public async Task<BookLookupResult?> LookupByIsbnAsync(string isbn)
        {
            var results = await LookupByIsbnsAsync([isbn]);
            return results[0];
        }

        public async Task<BookLookupCandidates> LookupCandidatesByIsbnAsync(
            string isbn)
        {
            var results = await LookupCandidatesByIsbnsAsync([isbn]);
            return results[0];
        }

        public async Task<IReadOnlyList<BookLookupCandidates>>
            LookupCandidatesByIsbnsAsync(IReadOnlyList<string> isbns)
        {
            var sources = await LookupSourcesByIsbnsAsync(isbns, maxNdlRecords: 20);
            return sources.Select(source =>
                new BookLookupCandidates
                {
                    Results = source.NormalizedIsbn == null
                        ? []
                        : BuildCandidates(
                            source.OpenBd,
                            source.NdlResults,
                            source.NormalizedIsbn),
                    NdlLookupFailed = source.NdlLookupFailed
                })
                .ToList();
        }

        public async Task<IReadOnlyList<BookLookupResult?>> LookupByIsbnsAsync(
            IReadOnlyList<string> isbns)
        {
            var sources = await LookupSourcesByIsbnsAsync(isbns, maxNdlRecords: 10);
            return sources.Select(source =>
            {
                if (source.NormalizedIsbn == null)
                    return source.OpenBd;

                var ndlResult = source.NdlResults.FirstOrDefault(x =>
                    OpenBDLookupService.NormalizeIsbn13(x.ISBN) ==
                    source.NormalizedIsbn);
                return ndlResult == null
                    ? source.OpenBd
                    : Merge(source.OpenBd, ndlResult, source.NormalizedIsbn);
            }).ToList();
        }

        internal static BookLookupResult Merge(
            BookLookupResult? openBd,
            NdlSearchResult ndl,
            string normalizedIsbn)
        {
            return new BookLookupResult
            {
                Title = FirstNonEmpty(ndl.Title, openBd?.Title),
                Creator = FirstNonEmpty(ndl.Creator, openBd?.Creator),
                Publisher = FirstNonEmpty(ndl.Publisher, openBd?.Publisher),
                ReleaseDate = OpenBDLookupService.ParsePublicationDate(ndl.PublicationDate)
                    ?? openBd?.ReleaseDate,
                CoverImageUrl = openBd?.CoverImageUrl,
                ISBN = OpenBDLookupService.NormalizeIsbn13(ndl.ISBN)
                    ?? OpenBDLookupService.NormalizeIsbn13(openBd?.ISBN)
                    ?? normalizedIsbn,
                PageCount = openBd?.PageCount
            };
        }

        private static string? FirstNonEmpty(string? preferred, string? fallback)
            => string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

        /// <summary>
        /// 通常取得と候補取得に共通する外部API処理を担う。
        /// openBDは一括取得し、NDLは入力順を保ったまま最大4並列で検索する。
        /// </summary>
        private async Task<IReadOnlyList<BookLookupSources>> LookupSourcesByIsbnsAsync(
            IReadOnlyList<string> isbns,
            int maxNdlRecords)
        {
            var openBdResults = await _openBd.LookupByIsbnsAsync(isbns);
            var results = new BookLookupSources[isbns.Count];
            using var semaphore = new SemaphoreSlim(MaxConcurrentNdlRequests);

            var tasks = isbns.Select(async (isbn, index) =>
            {
                var normalizedIsbn = OpenBDLookupService.NormalizeIsbn13(isbn);
                if (normalizedIsbn == null)
                {
                    results[index] = new BookLookupSources(
                        openBdResults[index],
                        null,
                        [],
                        NdlLookupFailed: false);
                    return;
                }

                await semaphore.WaitAsync();
                try
                {
                    var response = await _ndlSearch.SearchAsync(
                        new NdlSearchCriteria { ISBN = normalizedIsbn },
                        maxRecords: maxNdlRecords);
                    results[index] = new BookLookupSources(
                        openBdResults[index],
                        normalizedIsbn,
                        response.Succeeded ? response.Results : [],
                        NdlLookupFailed: !response.Succeeded);
                }
                catch (Exception ex)
                {
                    results[index] = new BookLookupSources(
                        openBdResults[index],
                        normalizedIsbn,
                        [],
                        NdlLookupFailed: true);
                    _logger.LogWarning(
                        ex,
                        "NDL lookup failed for ISBN {ISBN}.",
                        normalizedIsbn);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results;
        }

        private static IReadOnlyList<BookLookupResult> BuildCandidates(
            BookLookupResult? openBd,
            IReadOnlyList<NdlSearchResult> ndlResults,
            string normalizedIsbn)
        {
            var candidates = new List<BookLookupResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ndl in ndlResults)
            {
                if (OpenBDLookupService.NormalizeIsbn13(ndl.ISBN) != normalizedIsbn)
                    continue;

                var candidate = Merge(openBd, ndl, normalizedIsbn);
                if (string.IsNullOrWhiteSpace(candidate.Title))
                    continue;

                var key = string.Join(
                    "\u001f",
                    candidate.Title,
                    candidate.Creator,
                    candidate.Publisher,
                    candidate.ReleaseDate?.ToString("O"));
                if (seen.Add(key))
                    candidates.Add(candidate);
            }

            if (candidates.Count > 0)
                return candidates;

            return openBd == null || string.IsNullOrWhiteSpace(openBd.Title)
                ? []
                : [openBd];
        }

        private sealed record BookLookupSources(
            BookLookupResult? OpenBd,
            string? NormalizedIsbn,
            IReadOnlyList<NdlSearchResult> NdlResults,
            bool NdlLookupFailed);
    }
}
