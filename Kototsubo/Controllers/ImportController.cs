using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using Dev.CommonLibrary.Common;
using Site.Common;
using Site.Entity;
using Site.Models;
using Site.Repository;
using Site.Services;

namespace Site.Controllers
{
    [Authorize]
    public class ImportController : Controller
    {
        private const long MaxFileSize = 10 * 1024 * 1024;
        internal const long MaxIsbnFileSize = 1024 * 1024;
        internal const int MaxIsbnCount = KindleImportParser.MaxItemCount;
        private readonly ItemRepository _repository;
        private readonly KindleImportParser _parser;
        private readonly IBookCandidateLookupService _bookCandidateLookupService;
        private readonly INdlSearchService _ndlSearchService;
        private readonly ILogger<ImportController> _logger;
        private readonly string _tempDirectory;

        public ImportController(
            ItemRepository repository,
            KindleImportParser parser,
            IBookCandidateLookupService bookCandidateLookupService,
            INdlSearchService ndlSearchService,
            IWebHostEnvironment environment,
            ILogger<ImportController> logger)
        {
            _repository = repository;
            _parser = parser;
            _bookCandidateLookupService = bookCandidateLookupService;
            _ndlSearchService = ndlSearchService;
            _logger = logger;
            _tempDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "KindleImports");
        }

        [HttpGet]
        public IActionResult Kindle()
        {
            return View(new KindleImportViewModel());
        }

        [HttpGet]
        public IActionResult Isbn()
        {
            return View(new IsbnImportViewModel());
        }

        // 1 ページあたりの検索結果表示件数。
        internal const int TitleSearchPageSize = 20;

        // タイトル検索は GET ベース。検索条件・ページ番号を URL クエリに持たせることで、
        // ページング・リロード・プレビューからの戻りで状態が保持される。
        [HttpGet]
        public async Task<IActionResult> TitleSearch(TitleSearchViewModel model, int page = 1)
        {
            // 初回アクセス（検索未実行）は空フォームを表示する。
            if (!model.Searched)
            {
                return View(new TitleSearchViewModel());
            }

            // 検索実行だが条件未入力・不正値の場合はエラー表示（検索しない）。
            if (!ModelState.IsValid)
            {
                model.HasSearched = false;
                return View(model);
            }

            page = Math.Max(1, page);
            if (model.SortOrder != NdlSearchSortOrder.Default)
            {
                page = Math.Min(
                    page,
                    NdlSearchService.MaxSortableRecords / TitleSearchPageSize);
            }
            model.Page = page;
            var startRecord = (page - 1) * TitleSearchPageSize + 1;

            var response = await _ndlSearchService.SearchAsync(
                model.ToCriteria(), startRecord, TitleSearchPageSize);
            model.TotalResults = response.TotalResults;
            model.ResultsLimited = response.IsTruncated;
            model.SearchFailed = !response.Succeeded;
            model.HasSearched = true;

            // 検索結果の ISBN を正規化し、既に蔵品として登録済みのものを抽出する。
            // 登録済みの本は検索結果一覧で非活性（選択不可）表示にする。
            var resultIsbns = response.Results
                .Select(r => OpenBDLookupService.NormalizeIsbn13(r.ISBN))
                .Where(x => x != null)
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var registeredIsbns = _repository.GetBaseQuery()
                .Where(x => x.ISBN != null && resultIsbns.Contains(x.ISBN))
                .Select(x => x.ISBN!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            model.Results = response.Results.Select(r =>
            {
                var normalized = OpenBDLookupService.NormalizeIsbn13(r.ISBN);
                return new TitleSearchResultRow
                {
                    Title = r.Title,
                    Creator = r.Creator,
                    Publisher = r.Publisher,
                    ISBN = r.ISBN,
                    PublicationDate = r.PublicationDate,
                    IsAlreadyRegistered = normalized != null && registeredIsbns.Contains(normalized)
                };
            }).ToList();

            // ページャ表示用サマリー（総件数・現在ページの表示範囲）を構築する。
            var firstRecord = model.TotalResults == 0 ? 0 : startRecord;
            var endRecord = model.TotalResults == 0
                ? 0
                : startRecord + model.Results.Count - 1;
            model.Summary = new CommonListSummaryModel(
                page, model.TotalResults, firstRecord, endRecord,
                $"{model.TotalResults}件中 {firstRecord}～{endRecord}件を表示");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TitleSearchConfirm(
            List<string> selectedIsbns,
            List<string> selectedNoIsbn,
            TitleSearchViewModel search,
            string? selectedRecordsJson,
            string? noIsbnRecordsJson)
        {
            // タイトル検索フローも手入力 ISBN フローと同じ共通パイプラインを通す。
            // 選択された ISBN からプレビューを構築し、確定は共通の IsbnConfirm に委譲する。
            selectedIsbns ??= new List<string>();
            selectedNoIsbn ??= new List<string>();
            if (selectedIsbns.Count == 0 && selectedNoIsbn.Count == 0)
            {
                var emptyResult = new IsbnImportResultViewModel
                {
                    ContinueAction = "TitleSearch"
                };
                emptyResult.Errors.Add("登録する本を選択してください。");
                return View("IsbnResult", emptyResult);
            }

            var inputs = selectedIsbns
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxIsbnCount)
                .ToList();
            var titleSearchBooks = ParseTitleSearchSelections(selectedRecordsJson, inputs);

            // ISBN なし選択は件数上限を ISBN 選択と合算する（TitleSearch は同一パイプラインの1入口のため）。
            var noIsbnKeys = selectedNoIsbn
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, MaxIsbnCount - inputs.Count))
                .ToList();
            var noIsbnBooks = ParseNoIsbnSelections(noIsbnRecordsJson, noIsbnKeys);

            try
            {
                var preview = await BuildIsbnPreviewAsync(
                    inputs,
                    backAction: "TitleSearch",
                    titleSearchBooks,
                    noIsbnBooks);
                // 「戻る」で検索条件・ページを再現できるよう、条件付き URL を組み立てる。
                preview.BackUrl = Url.Action("TitleSearch", new
                {
                    search.Title,
                    search.Creator,
                    search.Publisher,
                    search.YearFrom,
                    search.YearTo,
                    search.SortOrder,
                    Searched = true,
                    page = Math.Max(1, search.Page)
                });
                return View("IsbnPreview", preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Title search preview snapshot failed.");
                var errorResult = new IsbnImportResultViewModel
                {
                    ContinueAction = "TitleSearch"
                };
                errorResult.Errors.Add("確認データの作成中にエラーが発生しました。");
                return View("IsbnResult", errorResult);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(
            ValueLengthLimit = IsbnImportViewModel.MaxInputLength + 1,
            MultipartBodyLengthLimit = MaxIsbnFileSize + 65536)]
        public async Task<IActionResult> Isbn(IsbnImportViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var hasText = !string.IsNullOrWhiteSpace(model.Isbns);
            var hasFile = model.File != null;
            if (hasText && hasFile)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "テキスト入力とファイルは、どちらか一方だけ指定してください。");
                return View(model);
            }

            if (!hasText && !hasFile)
            {
                ModelState.AddModelError(string.Empty, "ISBNを入力するか、ファイルを選択してください。");
                return View(model);
            }

            var inputText = model.Isbns;
            if (model.File != null)
            {
                try
                {
                    inputText = await ReadIsbnFileAsync(model.File);
                }
                catch (InvalidDataException ex)
                {
                    ModelState.AddModelError(nameof(model.File), ex.Message);
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ISBN import file read failed.");
                    ModelState.AddModelError(
                        nameof(model.File),
                        "ISBNファイルを読み込めませんでした。");
                    return View(model);
                }
            }

            var inputs = SplitIsbns(inputText);
            if (inputs.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Isbns), "ISBNを入力してください。");
                return View(model);
            }

            if (inputs.Count > MaxIsbnCount)
            {
                ModelState.AddModelError(
                    nameof(model.Isbns),
                    $"ISBNは{MaxIsbnCount}件以内で入力してください。");
                return View(model);
            }

            try
            {
                var preview = await BuildIsbnPreviewAsync(inputs, backAction: "Isbn");
                return View("IsbnPreview", preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ISBN import preview snapshot failed.");
                ModelState.AddModelError(
                    nameof(model.Isbns),
                    "確認データの作成中にエラーが発生しました。");
                return View(model);
            }
        }

        /// <summary>
        /// ISBN 群から書誌を引き、重複/ASIN 照合とプレビュー用スナップショットを構築する。
        /// 手入力 ISBN フロー・タイトル検索フローなど、全登録フローで共有する中核処理。
        /// 登録候補が 1 件以上ある場合は一時スナップショットを保存し、
        /// セッションに所有権トークンを記録した上で <see cref="IsbnImportPreviewViewModel.TempFilePath"/> を設定する。
        /// </summary>
        /// <param name="inputs">登録候補の ISBN（生文字列。正規化は内部で行う）。</param>
        /// <param name="backAction">プレビューの「戻る」リンク先アクション名（呼び出し元の入口画面）。</param>
        /// <param name="noIsbnBooks">
        /// タイトル検索で選択された ISBN なし書籍。ISBN がなく再照会できないため、
        /// 選択時点の書誌情報をそのまま唯一の候補として使う。
        /// </param>
        /// <returns>プレビュー表示用 ViewModel。</returns>
        /// <remarks>
        /// 呼び出し元固有のバリデーション・エラービュー描画はこのメソッドに含めない。
        /// スナップショット保存に失敗した場合は一時ファイルを削除した上で例外を再送出するため、
        /// 呼び出し元が自身のエラー面を描画すること。
        /// </remarks>
        private async Task<IsbnImportPreviewViewModel> BuildIsbnPreviewAsync(
            IReadOnlyList<string> inputs,
            string backAction,
            IReadOnlyDictionary<string, BookLookupResult>? titleSearchBooks = null,
            IReadOnlyList<BookLookupResult>? noIsbnBooks = null)
        {
            var candidateLookups =
                await _bookCandidateLookupService.LookupCandidatesByIsbnsAsync(inputs);
            var normalizedIsbns = candidateLookups
                .SelectMany(x => x.Results)
                .Where(x => x.ISBN != null)
                .Select(x => x.ISBN!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var amazonAsinCandidates = normalizedIsbns
                .Select(OpenBDLookupService.ToAmazonAsinCandidate)
                .Where(x => x != null)
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingIsbns = _repository.GetBaseQuery()
                .Where(x => x.ISBN != null && normalizedIsbns.Contains(x.ISBN))
                .Select(x => x.ISBN!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingAmazonAsins = _repository.GetBaseQuery()
                .Where(x => x.ASIN != null && amazonAsinCandidates.Contains(x.ASIN))
                .Select(x => x.ASIN!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seenIsbns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preview = new IsbnImportPreviewViewModel { BackAction = backAction };
            var snapshot = new IsbnImportSnapshot { SourceAction = backAction };

            for (var i = 0; i < inputs.Count; i++)
            {
                var lookup = candidateLookups[i];
                var lookupCandidates = lookup.Results;
                var firstValidationError = lookupCandidates
                    .Select(GetBookValidationError)
                    .FirstOrDefault(x => x != null);
                var candidates = lookupCandidates
                    .Where(x => GetBookValidationError(x) == null)
                    .ToList();
                BookLookupResult? titleSearchBook = null;
                var normalizedInput = OpenBDLookupService.NormalizeIsbn13(inputs[i]);
                var primaryBook = candidates.FirstOrDefault();
                if (primaryBook != null &&
                    normalizedInput != null &&
                    titleSearchBooks?.TryGetValue(normalizedInput, out var selectedBook) == true)
                {
                    titleSearchBook = new BookLookupResult
                    {
                        ISBN = normalizedInput,
                        Title = selectedBook.Title,
                        Creator = selectedBook.Creator,
                        Publisher = selectedBook.Publisher,
                        ReleaseDate = selectedBook.ReleaseDate,
                        CoverImageUrl = primaryBook.CoverImageUrl,
                        PageCount = primaryBook.PageCount
                    };
                    candidates.Insert(0, titleSearchBook);
                }
                candidates = DistinctCandidates(candidates);
                var book = candidates.FirstOrDefault();
                var row = new IsbnImportRow
                {
                    Input = inputs[i],
                    Key = book?.ISBN ?? string.Empty,
                    Book = book,
                    Candidates = candidates,
                    AmazonAsinCandidate = OpenBDLookupService.ToAmazonAsinCandidate(book?.ISBN)
                };
                row.HasLocalAmazonMatch = row.AmazonAsinCandidate != null &&
                    existingAmazonAsins.Contains(row.AmazonAsinCandidate);

                if (book?.ISBN == null)
                {
                    row.Error = BuildLookupErrorMessage(
                        inputs[i],
                        firstValidationError,
                        lookup.NdlLookupFailed);
                }
                else if (existingIsbns.Contains(book.ISBN) || !seenIsbns.Add(book.ISBN))
                {
                    row.IsDuplicate = true;
                }
                else
                {
                    preview.SelectedKeys.Add(book.ISBN);
                    preview.SelectedCandidateIndexes[book.ISBN] = 0;
                    snapshot.Rows.Add(new IsbnImportSnapshotRow
                    {
                        Key = book.ISBN,
                        ISBN = book.ISBN,
                        Candidates = candidates,
                        AmazonAsinCandidate = row.AmazonAsinCandidate
                    });
                }

                preview.Rows.Add(row);
            }

            // ISBN なしのタイトル検索選択。ISBN を持たないため候補は常に1件
            // （検索時点の書誌情報そのもの）で、外部再照会は行わない。
            if (noIsbnBooks != null)
            {
                var seenNoIsbnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var book in noIsbnBooks)
                {
                    var validationError = GetBookValidationError(book);
                    var row = new IsbnImportRow
                    {
                        Input = book.Title ?? string.Empty,
                        Error = validationError
                    };

                    if (validationError == null)
                    {
                        var dedupeKey = string.Join(
                            "", book.Title, book.Creator, book.Publisher,
                            book.ReleaseDate?.ToString("O"));
                        if (!seenNoIsbnKeys.Add(dedupeKey))
                        {
                            row.IsDuplicate = true;
                        }
                        else
                        {
                            var key = $"noisbn:{Guid.NewGuid():N}";
                            var candidates = new List<BookLookupResult> { book };
                            row.Key = key;
                            row.Book = book;
                            row.Candidates = candidates;
                            preview.SelectedKeys.Add(key);
                            preview.SelectedCandidateIndexes[key] = 0;
                            snapshot.Rows.Add(new IsbnImportSnapshotRow
                            {
                                Key = key,
                                ISBN = null,
                                Candidates = candidates
                            });
                        }
                    }

                    preview.Rows.Add(row);
                }
            }

            if (snapshot.Rows.Count == 0)
                return preview;

            var token = $"{Guid.NewGuid():N}.isbn.json";
            var tempPath = ResolveTempPath(token);
            try
            {
                Directory.CreateDirectory(_tempDirectory);
                await using var output = System.IO.File.Create(tempPath);
                await JsonSerializer.SerializeAsync(output, snapshot);
                HttpContext.Session.SetString(GetIsbnSessionKey(token), "owned");
                preview.TempFilePath = token;
                return preview;
            }
            catch
            {
                DeleteTempFile(tempPath);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = MaxIsbnCount * 2 + 4)]
        public async Task<IActionResult> IsbnConfirm(IsbnImportPreviewViewModel model)
        {
            var result = new IsbnImportResultViewModel
            {
                ContinueAction = model.BackAction == "TitleSearch"
                    ? "TitleSearch"
                    : "Isbn"
            };
            string tempPath;
            try
            {
                tempPath = ResolveTempPath(model.TempFilePath);
                if (HttpContext.Session.GetString(GetIsbnSessionKey(model.TempFilePath!)) != "owned")
                    throw new InvalidOperationException("Import token is not owned by this session.");
            }
            catch (InvalidOperationException)
            {
                result.Errors.Add("確認データが無効です。もう一度検索してください。");
                return View("IsbnResult", result);
            }

            if (!System.IO.File.Exists(tempPath))
            {
                HttpContext.Session.Remove(GetIsbnSessionKey(model.TempFilePath!));
                result.Errors.Add("確認データの有効期限が切れました。もう一度検索してください。");
                return View("IsbnResult", result);
            }

            var selectedKeys = model.SelectedKeys
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxIsbnCount)
                .ToList();

            try
            {
                await using var input = System.IO.File.OpenRead(tempPath);
                var snapshot = await JsonSerializer.DeserializeAsync<IsbnImportSnapshot>(input)
                    ?? new IsbnImportSnapshot();
                result.ContinueAction = snapshot.SourceAction == "TitleSearch"
                    ? "TitleSearch"
                    : "Isbn";
                var allowedRows = snapshot.Rows
                    .Where(x => selectedKeys.Contains(x.Key))
                    .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();
                // ISBN なし行（IsbnImportSnapshotRow.ISBN == null）はDB重複判定の対象外。
                var allowedIsbns = allowedRows
                    .Where(x => x.ISBN != null)
                    .Select(x => x.ISBN!)
                    .ToList();
                var existingIsbns = _repository.GetBaseQuery()
                    .Where(x => x.ISBN != null && allowedIsbns.Contains(x.ISBN))
                    .Select(x => x.ISBN!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                result.SkippedCount += selectedKeys.Count - allowedRows.Count;
                foreach (var row in allowedRows)
                {
                    var book = ResolveSelectedBook(row, model.SelectedCandidateIndexes);
                    if (book == null)
                    {
                        result.SkippedCount++;
                        continue;
                    }
                    if (GetBookValidationError(book) != null ||
                        (book.ISBN != null && existingIsbns.Contains(book.ISBN)))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        _repository.Insert(ToEntity(book, row.AmazonAsinCandidate));
                        if (book.ISBN != null) existingIsbns.Add(book.ISBN);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "ISBN item import failed for ISBN {ISBN}.",
                            book.ISBN);
                        result.Errors.Add($"「{book.Title}」を登録できませんでした。");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "ISBN import snapshot was invalid.");
                result.Errors.Add("確認データを読み込めませんでした。もう一度検索してください。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ISBN import confirmation failed.");
                result.Errors.Add("ISBN一括登録中にエラーが発生しました。");
            }
            finally
            {
                DeleteTempFile(tempPath);
                HttpContext.Session.Remove(GetIsbnSessionKey(model.TempFilePath!));
            }

            return View("IsbnResult", result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Kindle(KindleImportViewModel model)
        {
            if (!ModelState.IsValid || model.File == null)
                return View(model);

            var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();
            if (extension is not ".csv" and not ".json")
            {
                ModelState.AddModelError(nameof(model.File), "CSV または JSON ファイルを選択してください。");
                return View(model);
            }

            if (model.File.Length == 0)
            {
                ModelState.AddModelError(nameof(model.File), "ファイルが空です。");
                return View(model);
            }

            if (model.File.Length > MaxFileSize)
            {
                ModelState.AddModelError(nameof(model.File), "ファイルサイズは10MB以下にしてください。");
                return View(model);
            }

            var snapshotToken = $"{Guid.NewGuid():N}.json";
            var snapshotPath = ResolveTempPath(snapshotToken);

            try
            {
                Directory.CreateDirectory(_tempDirectory);
                List<KindleImportRow> rows;
                await using (var upload = model.File.OpenReadStream())
                {
                    rows = _parser.Parse(upload, extension).ToList();
                }

                await EnrichWithBookLookupAsync(rows);
                MarkDuplicates(rows);

                var snapshot = new KindleImportSnapshot { Rows = rows };
                await using (var output = System.IO.File.Create(snapshotPath))
                {
                    await JsonSerializer.SerializeAsync(output, snapshot);
                }

                HttpContext.Session.SetString(GetSessionKey(snapshotToken), "owned");

                return View("KindlePreview", new KindleImportPreviewViewModel
                {
                    Rows = rows,
                    SkipCount = rows.Count(x => x.IsDuplicate),
                    EnrichmentFailedCount = rows.Count(x => x.EnrichmentFailed),
                    TempFilePath = snapshotToken,
                    SelectedAsins = rows
                        .Where(x => !x.IsDuplicate && x.ASIN != null)
                        .Select(x => x.ASIN!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                });
            }
            catch (KindleImportException ex)
            {
                DeleteTempFile(snapshotPath);
                ModelState.AddModelError(nameof(model.File), ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                DeleteTempFile(snapshotPath);
                _logger.LogError(ex, "Kindle import preview failed.");
                ModelState.AddModelError(
                    nameof(model.File),
                    "ファイルの処理中にエラーが発生しました。");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = KindleImportParser.MaxItemCount + 2)]
        public async Task<IActionResult> KindleConfirm(KindleImportPreviewViewModel model)
        {
            var result = new KindleImportResultViewModel();
            string tempPath;

            try
            {
                tempPath = ResolveTempPath(model.TempFilePath);
                if (HttpContext.Session.GetString(GetSessionKey(model.TempFilePath!)) != "owned")
                    throw new InvalidOperationException("Import token is not owned by this session.");
            }
            catch (InvalidOperationException)
            {
                result.Errors.Add("確認用ファイルが無効です。もう一度アップロードしてください。");
                return View("KindleResult", result);
            }

            if (!System.IO.File.Exists(tempPath))
            {
                result.Errors.Add("確認用ファイルの有効期限が切れました。もう一度アップロードしてください。");
                return View("KindleResult", result);
            }

            try
            {
                await using var input = System.IO.File.OpenRead(tempPath);
                var snapshot = await JsonSerializer.DeserializeAsync<KindleImportSnapshot>(input)
                    ?? new KindleImportSnapshot();
                var selectedAsins = model.SelectedAsins
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingAsins = _repository.GetBaseQuery()
                    .Where(x => x.ASIN != null && selectedAsins.Contains(x.ASIN))
                    .Select(x => x.ASIN!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var row in snapshot.Rows)
                {
                    if (row.ASIN == null || !selectedAsins.Contains(row.ASIN))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    if (existingAsins.Contains(row.ASIN))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        _repository.Insert(ToEntity(row));
                        existingAsins.Add(row.ASIN);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Kindle item import failed for ASIN {ASIN}.", row.ASIN);
                        result.Errors.Add($"「{row.Title}」を登録できませんでした。");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Kindle import snapshot was invalid.");
                result.Errors.Add("確認データを読み込めませんでした。もう一度アップロードしてください。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kindle import confirmation failed.");
                result.Errors.Add("一括登録中にエラーが発生しました。");
            }
            finally
            {
                DeleteTempFile(tempPath);
                HttpContext.Session.Remove(GetSessionKey(model.TempFilePath!));
            }

            return View("KindleResult", result);
        }

        private void MarkDuplicates(List<KindleImportRow> rows)
        {
            var asins = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.ASIN))
                .Select(x => x.ASIN!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingAsins = _repository.GetBaseQuery()
                .Where(x => x.ASIN != null && asins.Contains(x.ASIN))
                .Select(x => x.ASIN!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seenAsins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                row.IsDuplicate = row.ASIN == null ||
                    existingAsins.Contains(row.ASIN) ||
                    !seenAsins.Add(row.ASIN);
            }
        }

        internal static ItemEntity ToEntity(KindleImportRow row)
        {
            return new ItemEntity
            {
                Title = row.Title!,
                Creator = row.Creator,
                ASIN = row.ASIN,
                ISBN = row.ISBN,
                Publisher = row.Publisher,
                ReleaseDate = row.ReleaseDate,
                PageCount = row.PageCount,
                MediaType = MediaType.Book,
                IsDigital = true,
                OwnershipStatus = OwnershipStatus.Owned,
                AcquisitionDate = DateTime.Today,
                CoverImageUrl = row.ThumbnailUrl,
                Memo = BuildMemo(row)
            };
        }

        internal static ItemEntity ToEntity(BookLookupResult book, string? asinCandidate = null)
        {
            var coverUrl = book.CoverImageUrl;
            if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(asinCandidate))
                coverUrl = OpenBDLookupService.GetAmazonCoverUrl(asinCandidate);

            return new ItemEntity
            {
                Title = book.Title!,
                Creator = book.Creator,
                Publisher = book.Publisher,
                ISBN = book.ISBN,
                ASIN = asinCandidate,
                MediaType = MediaType.Book,
                OwnershipStatus = OwnershipStatus.Owned,
                AcquisitionDate = DateTime.Today,
                ReleaseDate = book.ReleaseDate,
                CoverImageUrl = coverUrl,
                PageCount = book.PageCount
            };
        }

        internal static List<string> SplitIsbns(string? value)
        {
            return (value ?? string.Empty)
                .Split(
                    [',', '，', '、', '\r', '\n', '\t', ' ', '　'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        internal static async Task<string> ReadIsbnFileAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".txt" and not ".csv")
                throw new InvalidDataException("TXTまたはCSVファイルを選択してください。");
            if (file.Length == 0)
                throw new InvalidDataException("ファイルが空です。");
            if (file.Length > MaxIsbnFileSize)
                throw new InvalidDataException("ISBNファイルは1MB以下にしてください。");

            try
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                if (buffer.Length > MaxIsbnFileSize)
                    throw new InvalidDataException("ISBNファイルは1MB以下にしてください。");

                var bytes = buffer.ToArray();
                if (HasNonUtf8Bom(bytes))
                    throw new InvalidDataException("ファイルをUTF-8で保存してください。");

                var offset = bytes.Length >= 3 &&
                    bytes[0] == 0xEF &&
                    bytes[1] == 0xBB &&
                    bytes[2] == 0xBF
                    ? 3
                    : 0;
                var content = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                    .GetString(bytes, offset, bytes.Length - offset);
                using var reader = new StringReader(content);
                var values = new List<string>();
                var lineNumber = 0;

                while (reader.ReadLine() is { } line)
                {
                    lineNumber++;
                    var value = line.Trim();
                    if (string.IsNullOrEmpty(value)) continue;

                    if (string.Equals(value, "ISBN", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("ヘッダー行は使用できません。1行目からISBNを記録してください。");
                    if (!value.All(c => char.IsDigit(c) || c is '-' or 'X' or 'x'))
                    {
                        throw new InvalidDataException(
                            $"{lineNumber}行目の形式が不正です。1行にISBNを1件だけ記録してください。");
                    }

                    values.Add(value);
                    if (values.Count > MaxIsbnCount)
                        throw new InvalidDataException($"ISBNは{MaxIsbnCount}件以内にしてください。");
                }

                if (values.Count == 0)
                    throw new InvalidDataException("ファイルにISBNがありません。");

                return string.Join(Environment.NewLine, values);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("ファイルをUTF-8で保存してください。");
            }
        }

        private static bool HasNonUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 2 &&
                    ((bytes[0] == 0xFF && bytes[1] == 0xFE) ||
                     (bytes[0] == 0xFE && bytes[1] == 0xFF)) ||
                bytes.Length >= 4 &&
                    ((bytes[0] == 0x00 && bytes[1] == 0x00 &&
                      bytes[2] == 0xFE && bytes[3] == 0xFF) ||
                     (bytes[0] == 0xFF && bytes[1] == 0xFE &&
                      bytes[2] == 0x00 && bytes[3] == 0x00));
        }

        private static string? GetBookValidationError(BookLookupResult book)
        {
            if (string.IsNullOrWhiteSpace(book.Title))
                return "タイトルを取得できません。";
            if (book.Title.Length > 500)
                return "タイトルが登録可能な長さを超えています。";
            if (book.Creator?.Length > 500)
                return "著者名が登録可能な長さを超えています。";
            if (book.Publisher?.Length > 500)
                return "出版社名が登録可能な長さを超えています。";
            if (book.CoverImageUrl?.Length > 1000)
                return "カバー画像URLが登録可能な長さを超えています。";

            return null;
        }

        internal static string BuildLookupErrorMessage(
            string input,
            string? validationError,
            bool ndlLookupFailed)
        {
            if (validationError != null)
                return validationError;
            if (OpenBDLookupService.NormalizeIsbn13(input) == null)
                return "無効なISBNです。";
            return ndlLookupFailed
                ? "書誌情報の取得に失敗しました。しばらくしてから再度お試しください。"
                : "書誌情報が見つかりません。";
        }

        internal static IReadOnlyDictionary<string, BookLookupResult> ParseTitleSearchSelections(
            string? json,
            IReadOnlyCollection<string> selectedIsbns)
        {
            if (string.IsNullOrWhiteSpace(json) || selectedIsbns.Count == 0)
                return new Dictionary<string, BookLookupResult>();

            List<TitleSearchSelectionInput>? records;
            try
            {
                records = JsonSerializer.Deserialize<List<TitleSearchSelectionInput>>(json);
            }
            catch (JsonException)
            {
                return new Dictionary<string, BookLookupResult>();
            }

            if (records == null)
                return new Dictionary<string, BookLookupResult>();

            var allowedIsbns = selectedIsbns
                .Select(OpenBDLookupService.NormalizeIsbn13)
                .Where(x => x != null)
                .Select(x => x!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, BookLookupResult>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var record in records.Take(NdlSearchService.MaxSortableRecords))
            {
                var isbn = OpenBDLookupService.NormalizeIsbn13(record.ISBN);
                if (isbn == null ||
                    !allowedIsbns.Contains(isbn) ||
                    result.ContainsKey(isbn) ||
                    string.IsNullOrWhiteSpace(record.Title))
                {
                    continue;
                }

                var book = new BookLookupResult
                {
                    ISBN = isbn,
                    Title = record.Title.Trim(),
                    Creator = NullIfWhiteSpace(record.Creator),
                    Publisher = NullIfWhiteSpace(record.Publisher),
                    ReleaseDate = OpenBDLookupService.ParsePublicationDate(record.PublicationDate)
                };
                if (GetBookValidationError(book) == null)
                    result.Add(isbn, book);
            }

            return result;
        }

        /// <summary>
        /// タイトル検索で選択された ISBN なし書籍を、クライアントが返した合成キーで検証する。
        /// ISBN がなく外部再照会できないため、検索時点の書誌情報をそのまま採用する。
        /// </summary>
        internal static List<BookLookupResult> ParseNoIsbnSelections(
            string? json,
            IReadOnlyCollection<string> selectedKeys)
        {
            if (string.IsNullOrWhiteSpace(json) || selectedKeys.Count == 0)
                return new List<BookLookupResult>();

            List<NoIsbnSelectionInput>? records;
            try
            {
                records = JsonSerializer.Deserialize<List<NoIsbnSelectionInput>>(json);
            }
            catch (JsonException)
            {
                return new List<BookLookupResult>();
            }

            if (records == null)
                return new List<BookLookupResult>();

            var allowedKeys = selectedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<BookLookupResult>();

            foreach (var record in records.Take(NdlSearchService.MaxSortableRecords))
            {
                if (string.IsNullOrWhiteSpace(record.Key) ||
                    !allowedKeys.Contains(record.Key) ||
                    !seenKeys.Add(record.Key) ||
                    string.IsNullOrWhiteSpace(record.Title))
                {
                    continue;
                }

                var book = new BookLookupResult
                {
                    ISBN = null,
                    Title = record.Title.Trim(),
                    Creator = NullIfWhiteSpace(record.Creator),
                    Publisher = NullIfWhiteSpace(record.Publisher),
                    ReleaseDate = OpenBDLookupService.ParsePublicationDate(record.PublicationDate)
                };
                if (GetBookValidationError(book) == null)
                    result.Add(book);
            }

            return result;
        }

        internal static BookLookupResult? ResolveSelectedBook(
            IsbnImportSnapshotRow row,
            IReadOnlyDictionary<string, int> selectedCandidateIndexes)
        {
            if (string.IsNullOrWhiteSpace(row.Key) ||
                !selectedCandidateIndexes.TryGetValue(row.Key, out var selectedIndex) ||
                selectedIndex < 0 ||
                selectedIndex >= row.Candidates.Count)
            {
                return null;
            }

            var candidate = row.Candidates[selectedIndex];
            if (row.ISBN == null)
            {
                // ISBN なし書籍は候補が常に1件（タイトル検索選択時点の情報そのもの）のため、
                // インデックスが範囲内であれば候補の同一性はそれだけで保証される。
                return candidate;
            }

            // クライアントは候補番号しか送らないため、スナップショット内のISBN一致も
            // 再確認して別ISBNの候補へ差し替えられないようにする。
            return OpenBDLookupService.NormalizeIsbn13(candidate.ISBN) == row.ISBN
                ? candidate
                : null;
        }

        private static List<BookLookupResult> DistinctCandidates(
            IEnumerable<BookLookupResult> candidates)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return candidates.Where(candidate =>
            {
                var key = string.Join(
                    "\u001f",
                    candidate.Title,
                    candidate.Creator,
                    candidate.Publisher,
                    candidate.ReleaseDate?.ToString("O"));
                return seen.Add(key);
            }).ToList();
        }

        private static string? NullIfWhiteSpace(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? BuildMemo(KindleImportRow row)
        {
            var values = new List<string>();
            if (!string.IsNullOrWhiteSpace(row.Series))
                values.Add($"シリーズ: {row.Series}");
            if (row.Volume.HasValue)
                values.Add($"巻数: {row.Volume.Value}");
            if (!string.IsNullOrWhiteSpace(row.ReadStatus))
                values.Add($"既読ステータス: {row.ReadStatus}");

            var memo = string.Join(Environment.NewLine, values);
            return string.IsNullOrEmpty(memo) ? null : memo[..Math.Min(memo.Length, 2000)];
        }

        /// <summary>
        /// ISBN-10形式のASINを書誌検索し、NDLを基準に書誌情報を補完する。
        /// B0系など非ISBN ASINはスキップされる。
        /// </summary>
        internal async Task EnrichWithBookLookupAsync(List<KindleImportRow> rows)
        {
            var candidates = rows
                .Select((row, index) => new
                {
                    Index = index,
                    Isbn13 = OpenBDLookupService.NormalizeIsbn13(row.ASIN)
                })
                .Where(x => x.Isbn13 != null)
                .ToList();

            if (candidates.Count == 0) return;

            var isbns = candidates.Select(x => x.Isbn13!).ToList();
            IReadOnlyList<BookLookupCandidates> results;
            try
            {
                results = await _bookCandidateLookupService.LookupCandidatesByIsbnsAsync(isbns);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Book enrichment failed; proceeding without enrichment.");
                foreach (var candidate in candidates)
                {
                    rows[candidate.Index].EnrichmentFailed = true;
                }
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var lookup = results[i];
                var row = rows[candidates[i].Index];
                if (lookup.NdlLookupFailed)
                    row.EnrichmentFailed = true;

                var book = lookup.Results.FirstOrDefault();
                if (book == null) continue;

                row.ISBN = book.ISBN;
                if (!string.IsNullOrWhiteSpace(book.Title))
                    row.Title = book.Title;
                if (!string.IsNullOrWhiteSpace(book.Creator))
                    row.Creator = book.Creator;
                row.Publisher = book.Publisher;
                row.ReleaseDate = book.ReleaseDate;
                row.PageCount = book.PageCount;
                if (!string.IsNullOrWhiteSpace(book.CoverImageUrl) &&
                    string.IsNullOrWhiteSpace(row.ThumbnailUrl))
                {
                    row.ThumbnailUrl = book.CoverImageUrl;
                }
            }

            // 書誌検索でもカバーが取れなかった行に Amazon カバーを fallback
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.ThumbnailUrl) &&
                    !string.IsNullOrWhiteSpace(row.ASIN))
                {
                    row.ThumbnailUrl = OpenBDLookupService.GetAmazonCoverUrl(row.ASIN);
                }
            }
        }

        private string ResolveTempPath(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                token != Path.GetFileName(token) ||
                Path.GetExtension(token).ToLowerInvariant() is not ".csv" and not ".json")
            {
                throw new InvalidOperationException("Invalid import token.");
            }

            var fullDirectory = Path.GetFullPath(_tempDirectory) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(_tempDirectory, token));
            if (!fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid import path.");

            return fullPath;
        }

        private static void DeleteTempFile(string path)
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch
            {
                // Temporary-file cleanup must not replace the user-facing result.
            }
        }

        private static string GetSessionKey(string token)
        {
            return $"KindleImport:{token}";
        }

        private static string GetIsbnSessionKey(string token)
        {
            return $"IsbnImport:{token}";
        }
    }
}
