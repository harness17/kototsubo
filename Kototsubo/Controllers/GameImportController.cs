using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Site.Common;
using Site.Entity;
using Site.Models;
using Site.Repository;
using Site.Services;

namespace Site.Controllers
{
    /// <summary>
    /// ゲームソフトの JAN 一括登録フロー（入力 → プレビュー → 確定）。
    /// DVD/CD とは別系統の専門処理として独立実装しており、
    /// ISBN 系統（<see cref="ImportController"/>）とは共有しない。
    /// スナップショット保存・所有権トークン検証・パストラバーサル対策は
    /// 本コントローラ内で完結して実装する。
    /// </summary>
    [Authorize]
    public class GameImportController : Controller
    {
        internal const int MaxJanCount = 1000;
        private const long MaxFileSize = 1024 * 1024;

        private readonly IGameLookupService _lookup;
        private readonly ItemRepository _repository;
        private readonly ILogger<GameImportController> _logger;
        private readonly string _tempDirectory;

        public GameImportController(
            IGameLookupService lookup,
            ItemRepository repository,
            IWebHostEnvironment environment,
            ILogger<GameImportController> logger)
        {
            _lookup = lookup;
            _repository = repository;
            _logger = logger;
            _tempDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "GameImports");
        }

        // ゲームソフトの JAN 入力フォームを表示する
        [HttpGet]
        public IActionResult Index()
        {
            return View(new GameImportViewModel());
        }

        // 入力（テキスト or ファイル）から登録プレビューを構築する
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(
            ValueLengthLimit = GameImportViewModel.MaxInputLength + 1,
            MultipartBodyLengthLimit = MaxFileSize + 65536)]
        public async Task<IActionResult> Index(GameImportViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var hasText = !string.IsNullOrWhiteSpace(model.Jans);
            var hasFile = model.File != null;
            if (hasText && hasFile)
            {
                ModelState.AddModelError(
                    string.Empty, "テキスト入力とファイルは、どちらか一方だけ指定してください。");
                return View(model);
            }
            if (!hasText && !hasFile)
            {
                ModelState.AddModelError(
                    string.Empty, "JANコードを入力するか、ファイルを選択してください。");
                return View(model);
            }

            var inputText = model.Jans;
            if (model.File != null)
            {
                try
                {
                    inputText = await ReadJanFileAsync(model.File);
                }
                catch (InvalidDataException ex)
                {
                    ModelState.AddModelError(nameof(model.File), ex.Message);
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Game import file read failed.");
                    ModelState.AddModelError(nameof(model.File), "JANファイルを読み込めませんでした。");
                    return View(model);
                }
            }

            var inputs = SplitJans(inputText);
            if (inputs.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Jans), "JANコードを入力してください。");
                return View(model);
            }
            if (inputs.Count > MaxJanCount)
            {
                ModelState.AddModelError(
                    nameof(model.Jans), $"JANコードは{MaxJanCount}件以内で入力してください。");
                return View(model);
            }

            try
            {
                var preview = await BuildPreviewAsync(inputs);
                return View("Preview", preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game import preview snapshot failed.");
                ModelState.AddModelError(nameof(model.Jans), "確認データの作成中にエラーが発生しました。");
                return View(model);
            }
        }

        /// <summary>
        /// JAN 群からゲームソフトの書誌を引き、重複判定・プレビュー行・
        /// 一時スナップショットを構築する。登録候補が 1 件以上あるときのみ
        /// スナップショットを保存し、セッションに所有権トークンを記録する。
        /// </summary>
        private async Task<GameImportPreviewViewModel> BuildPreviewAsync(
            IReadOnlyList<string> inputs)
        {
            var lookups = await _lookup.LookupByJansAsync(inputs);
            var foundJans = lookups
                .Where(x => x?.Jan != null)
                .Select(x => x!.Jan!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingJans = _repository.GetBaseQuery()
                .Where(x => x.JANCode != null && foundJans.Contains(x.JANCode))
                .Select(x => x.JANCode!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seenJans = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var preview = new GameImportPreviewViewModel();
            var snapshot = new GameImportSnapshot();

            for (var i = 0; i < inputs.Count; i++)
            {
                var result = lookups[i];
                var row = new GameImportRow { Input = inputs[i], Result = result };

                if (result?.Jan == null)
                {
                    row.Error = "無効なJAN、またはゲーム情報が見つかりません。";
                }
                else if (GetValidationError(result) is { } validationError)
                {
                    row.Error = validationError;
                }
                else if (existingJans.Contains(result.Jan) || !seenJans.Add(result.Jan))
                {
                    row.IsDuplicate = true;
                }
                else
                {
                    preview.SelectedJans.Add(result.Jan);
                    snapshot.Rows.Add(new GameImportSnapshotRow { Result = result });
                }

                preview.Rows.Add(row);
            }

            if (snapshot.Rows.Count == 0) return preview;

            var token = $"{Guid.NewGuid():N}.json";
            var tempPath = ResolveTempPath(token);
            try
            {
                Directory.CreateDirectory(_tempDirectory);
                await using var output = System.IO.File.Create(tempPath);
                await JsonSerializer.SerializeAsync(output, snapshot);
                HttpContext.Session.SetString(GetSessionKey(token), "owned");
                preview.TempFilePath = token;
                return preview;
            }
            catch
            {
                DeleteTempFile(tempPath);
                throw;
            }
        }

        // プレビューで選択されたゲームソフトを一括登録する
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = MaxJanCount + 2)]
        public async Task<IActionResult> Confirm(GameImportPreviewViewModel model)
        {
            var result = new GameImportResultViewModel();
            string tempPath;
            try
            {
                tempPath = ResolveTempPath(model.TempFilePath);
                if (HttpContext.Session.GetString(GetSessionKey(model.TempFilePath!)) != "owned")
                    throw new InvalidOperationException("Import token is not owned by this session.");
            }
            catch (InvalidOperationException)
            {
                result.Errors.Add("確認データが無効です。もう一度検索してください。");
                return View("Result", result);
            }

            if (!System.IO.File.Exists(tempPath))
            {
                HttpContext.Session.Remove(GetSessionKey(model.TempFilePath!));
                result.Errors.Add("確認データの有効期限が切れました。もう一度検索してください。");
                return View("Result", result);
            }

            var selectedJans = model.SelectedJans
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxJanCount)
                .ToList();

            try
            {
                await using var input = System.IO.File.OpenRead(tempPath);
                var snapshot = await JsonSerializer.DeserializeAsync<GameImportSnapshot>(input)
                    ?? new GameImportSnapshot();
                var allowedRows = snapshot.Rows
                    .Where(x => x.Result.Jan != null && selectedJans.Contains(x.Result.Jan))
                    .GroupBy(x => x.Result.Jan!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
                var allowedJans = allowedRows.Select(x => x.Result.Jan!).ToList();
                var existingJans = _repository.GetBaseQuery()
                    .Where(x => x.JANCode != null && allowedJans.Contains(x.JANCode))
                    .Select(x => x.JANCode!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                result.SkippedCount += selectedJans.Count - allowedRows.Count;
                foreach (var row in allowedRows)
                {
                    var r = row.Result;
                    if (GetValidationError(r) != null || existingJans.Contains(r.Jan!))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        _repository.Insert(ToEntity(r));
                        existingJans.Add(r.Jan!);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Game item import failed for JAN {Jan}.", r.Jan);
                        result.Errors.Add($"JAN「{r.Jan}」を登録できませんでした。");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Game import snapshot was invalid.");
                result.Errors.Add("確認データを読み込めませんでした。もう一度検索してください。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Game import confirmation failed.");
                result.Errors.Add("ゲーム一括登録中にエラーが発生しました。");
            }
            finally
            {
                DeleteTempFile(tempPath);
                HttpContext.Session.Remove(GetSessionKey(model.TempFilePath!));
            }

            return View("Result", result);
        }

        /// <summary>ゲームの検索結果から蔵品エンティティを生成する（MediaType は Game 固定）。</summary>
        internal static ItemEntity ToEntity(GameLookupResult r)
        {
            return new ItemEntity
            {
                Title = r.Title ?? r.Jan ?? string.Empty,
                Publisher = r.Publisher,
                JANCode = r.Jan,
                MediaType = MediaType.Game,
                Platform = r.Platform,
                ReleaseDate = r.ReleaseDate,
                CoverImageUrl = r.CoverImageUrl,
                OwnershipStatus = OwnershipStatus.Owned,
                AcquisitionDate = DateTime.Today
            };
        }

        /// <summary>登録前バリデーション（長さ超過などを検出）。問題なければ null。</summary>
        private static string? GetValidationError(GameLookupResult r)
        {
            if (string.IsNullOrWhiteSpace(r.Title))
                return "タイトルを取得できません。";
            if (r.Title.Length > 500)
                return "タイトルが登録可能な長さを超えています。";
            if (r.Publisher?.Length > 500)
                return "メーカー名が登録可能な長さを超えています。";
            if (r.Platform?.Length > 100)
                return "プラットフォーム名が登録可能な長さを超えています。";
            if (r.CoverImageUrl?.Length > 1000)
                return "カバー画像URLが登録可能な長さを超えています。";
            return null;
        }

        /// <summary>区切り文字（改行・空白・カンマ・タブ・読点）で JAN を分割する。</summary>
        internal static List<string> SplitJans(string? value)
        {
            return (value ?? string.Empty)
                .Split(
                    [',', '、', '\r', '\n', '\t', ' '],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        /// <summary>アップロードされた TXT/CSV を UTF-8 で読み取り、全文を返す。</summary>
        private static async Task<string> ReadJanFileAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".txt" and not ".csv")
                throw new InvalidDataException("TXTまたはCSVファイルを選択してください。");
            if (file.Length == 0)
                throw new InvalidDataException("ファイルが空です。");
            if (file.Length > MaxFileSize)
                throw new InvalidDataException("JANファイルは1MB以下にしてください。");

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        /// <summary>トークンを検証し、一時ディレクトリ配下の絶対パスに解決する（パストラバーサル対策）。</summary>
        private string ResolveTempPath(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) ||
                token != Path.GetFileName(token) ||
                Path.GetExtension(token).ToLowerInvariant() != ".json")
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
                // 一時ファイルの削除失敗はユーザー向け結果を妨げない
            }
        }

        private static string GetSessionKey(string token) => $"GameImport:{token}";
    }
}
