namespace Site.Services
{
    /// <summary>
    /// 国立国会図書館サーチの書誌を優先し、openBD・楽天ブックスで付加情報と欠損項目を補完する。
    /// </summary>
    public class NdlPrimaryBookLookupService :
        IBookLookupService,
        IBookCandidateLookupService
    {
        private const int MaxConcurrentNdlRequests = 4;
        private readonly OpenBDLookupService _openBd;
        private readonly INdlSearchService _ndlSearch;
        private readonly RakutenBookLookupService? _rakuten;
        private readonly ILogger<NdlPrimaryBookLookupService> _logger;

        public NdlPrimaryBookLookupService(
            OpenBDLookupService openBd,
            INdlSearchService ndlSearch,
            ILogger<NdlPrimaryBookLookupService> logger,
            RakutenBookLookupService? rakuten = null)
        {
            _openBd = openBd;
            _ndlSearch = ndlSearch;
            _rakuten = rakuten;
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
                            source.NormalizedIsbn,
                            source.Rakuten),
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
                    return MergeWithRakutenFallback(source.OpenBd, source.Rakuten);

                var ndlResult = source.NdlResults.FirstOrDefault(x =>
                    OpenBDLookupService.NormalizeIsbn13(x.ISBN) ==
                    source.NormalizedIsbn);
                return ndlResult == null
                    ? MergeWithRakutenFallback(source.OpenBd, source.Rakuten)
                    : Merge(source.OpenBd, ndlResult, source.NormalizedIsbn, source.Rakuten);
            }).ToList();
        }

        internal static BookLookupResult Merge(
            BookLookupResult? openBd,
            NdlSearchResult ndl,
            string normalizedIsbn,
            BookLookupResult? rakuten = null)
        {
            return new BookLookupResult
            {
                Title = FirstNonEmpty(ndl.Title, openBd?.Title, rakuten?.Title),
                Creator = FirstNonEmpty(ndl.Creator, openBd?.Creator, rakuten?.Creator),
                Publisher = FirstNonEmpty(ndl.Publisher, openBd?.Publisher, rakuten?.Publisher),
                ReleaseDate = OpenBDLookupService.ParsePublicationDate(ndl.PublicationDate)
                    ?? openBd?.ReleaseDate
                    ?? rakuten?.ReleaseDate,
                // カバー画像: openBD → 楽天 の順で補完
                CoverImageUrl = FirstNonEmpty(openBd?.CoverImageUrl, rakuten?.CoverImageUrl),
                ISBN = OpenBDLookupService.NormalizeIsbn13(ndl.ISBN)
                    ?? OpenBDLookupService.NormalizeIsbn13(openBd?.ISBN)
                    ?? normalizedIsbn,
                PageCount = openBd?.PageCount ?? rakuten?.PageCount
            };
        }

        /// <summary>
        /// NDL結果がない場合に openBD と楽天ブックスの結果をマージして返す。
        /// どちらも null の場合は null を返す。
        /// </summary>
        private static BookLookupResult? MergeWithRakutenFallback(
            BookLookupResult? openBd, BookLookupResult? rakuten)
        {
            if (openBd == null && rakuten == null) return null;
            if (openBd == null) return rakuten;
            if (rakuten == null) return openBd;

            return new BookLookupResult
            {
                Title = FirstNonEmpty(openBd.Title, rakuten.Title),
                Creator = FirstNonEmpty(openBd.Creator, rakuten.Creator),
                Publisher = FirstNonEmpty(openBd.Publisher, rakuten.Publisher),
                ReleaseDate = openBd.ReleaseDate ?? rakuten.ReleaseDate,
                CoverImageUrl = FirstNonEmpty(openBd.CoverImageUrl, rakuten.CoverImageUrl),
                ISBN = openBd.ISBN ?? rakuten.ISBN,
                PageCount = openBd.PageCount ?? rakuten.PageCount
            };
        }

        private static string? FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        /// <summary>
        /// 通常取得と候補取得に共通する外部API処理を担う。
        /// openBDは一括取得し、NDLは入力順を保ったまま最大4並列で検索する。
        /// 楽天ブックスが設定済みの場合はカバー画像等の補完用に並行取得する。
        /// </summary>
        private async Task<IReadOnlyList<BookLookupSources>> LookupSourcesByIsbnsAsync(
            IReadOnlyList<string> isbns,
            int maxNdlRecords)
        {
            var openBdTask = _openBd.LookupByIsbnsAsync(isbns);
            var rakutenTask = LookupRakutenByIsbnsAsync(isbns);
            await Task.WhenAll(openBdTask, rakutenTask);
            var openBdResults = openBdTask.Result;
            var rakutenResults = rakutenTask.Result;

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
                        NdlLookupFailed: false,
                        rakutenResults[index]);
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
                        NdlLookupFailed: !response.Succeeded,
                        rakutenResults[index]);
                }
                catch (Exception ex)
                {
                    results[index] = new BookLookupSources(
                        openBdResults[index],
                        normalizedIsbn,
                        [],
                        NdlLookupFailed: true,
                        rakutenResults[index]);
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

        /// <summary>
        /// 楽天ブックスから各ISBNの書籍情報を取得する。未設定時は全要素 null を返す。
        /// </summary>
        private async Task<BookLookupResult?[]> LookupRakutenByIsbnsAsync(
            IReadOnlyList<string> isbns)
        {
            var results = new BookLookupResult?[isbns.Count];
            if (_rakuten == null || !_rakuten.IsConfigured)
                return results;

            // 楽天APIはレート制限があるため直列で取得する（RakutenBooksClient内部のセマフォに委譲）
            for (var i = 0; i < isbns.Count; i++)
            {
                try
                {
                    results[i] = await _rakuten.LookupByIsbnAsync(isbns[i]);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Rakuten book lookup failed for ISBN {ISBN}.",
                        isbns[i]);
                }
            }

            return results;
        }

        private static IReadOnlyList<BookLookupResult> BuildCandidates(
            BookLookupResult? openBd,
            IReadOnlyList<NdlSearchResult> ndlResults,
            string normalizedIsbn,
            BookLookupResult? rakuten = null)
        {
            var candidates = new List<BookLookupResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ndl in ndlResults)
            {
                if (OpenBDLookupService.NormalizeIsbn13(ndl.ISBN) != normalizedIsbn)
                    continue;

                var candidate = Merge(openBd, ndl, normalizedIsbn, rakuten);
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

            var fallback = MergeWithRakutenFallback(openBd, rakuten);
            return fallback == null || string.IsNullOrWhiteSpace(fallback.Title)
                ? []
                : [fallback];
        }

        private sealed record BookLookupSources(
            BookLookupResult? OpenBd,
            string? NormalizedIsbn,
            IReadOnlyList<NdlSearchResult> NdlResults,
            bool NdlLookupFailed,
            BookLookupResult? Rakuten);
    }
}
