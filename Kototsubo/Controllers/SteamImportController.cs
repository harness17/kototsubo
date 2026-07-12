using System.Globalization;
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
    /// Steam Titlelist Getter の JSON 一括登録フロー（入力 → プレビュー → 確定）。
    /// JAN 系 import とは別系統だが、スナップショット保存・所有権トークン検証・
    /// パストラバーサル対策は既存 import controller と同じ契約で実装する。
    /// </summary>
    [Authorize]
    public class SteamImportController : Controller
    {
        internal const int MaxGameCount = 1000;
        internal const long MaxFileSize = 5 * 1024 * 1024;
        // Steam appdetails は非公式・低いレート制限のため、既存の楽天/NDL連携と同じ同時実行数に揃える。
        private const int MaxConcurrentCoverLookups = 4;

        private readonly ItemRepository _repository;
        private readonly ISteamAppDetailsLookupService _coverLookup;
        private readonly ILogger<SteamImportController> _logger;
        private readonly string _tempDirectory;

        public SteamImportController(
            ItemRepository repository,
            ISteamAppDetailsLookupService coverLookup,
            IWebHostEnvironment environment,
            ILogger<SteamImportController> logger)
        {
            _repository = repository;
            _coverLookup = coverLookup;
            _logger = logger;
            _tempDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "SteamImports");
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new SteamImportViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize + 65536)]
        public async Task<IActionResult> Index(SteamImportViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            if (model.File == null)
            {
                ModelState.AddModelError(nameof(model.File), "Steam JSONファイルを選択してください。");
                return View(model);
            }

            SteamImportParseResult parsed;
            try
            {
                var json = await ReadSteamJsonFileAsync(model.File);
                parsed = ParseJson(json);
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(nameof(model.File), ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Steam import file read failed.");
                ModelState.AddModelError(nameof(model.File), "Steam JSONファイルを読み込めませんでした。");
                return View(model);
            }

            if (parsed.Rows.Count > MaxGameCount)
            {
                ModelState.AddModelError(nameof(model.File), $"Steamゲームは{MaxGameCount}件以内で入力してください。");
                return View(model);
            }

            try
            {
                var preview = await BuildPreviewAsync(parsed);
                return View("Preview", preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Steam import preview snapshot failed.");
                ModelState.AddModelError(nameof(model.File), "確認データの作成中にエラーが発生しました。");
                return View(model);
            }
        }

        private async Task<SteamImportPreviewViewModel> BuildPreviewAsync(SteamImportParseResult parsed)
        {
            var appIds = parsed.Rows
                .Select(x => x.AppId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var existingAppIds = _repository.GetBaseQuery()
                .Where(x => x.SteamAppId != null && appIds.Contains(x.SteamAppId))
                .Select(x => x.SteamAppId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var preview = new SteamImportPreviewViewModel
            {
                SteamId = parsed.SteamId,
                TotalGames = parsed.TotalGames
            };

            foreach (var entry in parsed.Rows)
            {
                preview.Rows.Add(new SteamImportRow
                {
                    AppId = entry.AppId,
                    Name = entry.Name,
                    PlaytimeMinutes = entry.PlaytimeMinutes,
                    CoverImageUrl = entry.CoverImageUrl,
                    Error = GetValidationError(entry)
                });
            }

            MarkDuplicates(preview.Rows, existingAppIds);

            // カバー画像は登録対象行に絞ってから解決する。全行を対象にすると
            // 登録済みライブラリの再インポートで数百件のバーストになり、Steam の
            // レート制限で新規行の解決まで失敗する（実測: 393件登録済みで全件null化）。
            var importableRows = preview.Rows.Where(CanImport).ToList();
            var coverImageUrls = await ResolveCoverImageUrlsAsync(
                importableRows.Select(x => x.AppId));

            var snapshot = new SteamImportSnapshot();
            foreach (var row in importableRows)
            {
                var coverUrl = SelectCoverImageUrl(row, coverImageUrls);
                // DB制約(1000文字)超過のURLは行ごとスキップせず画像なしに落とす。
                row.CoverImageUrl = coverUrl is { Length: <= 1000 } ? coverUrl : null;
                preview.SelectedAppIds.Add(row.AppId);
                snapshot.Rows.Add(new SteamImportSnapshotRow
                {
                    AppId = row.AppId,
                    Name = row.Name,
                    PlaytimeMinutes = row.PlaytimeMinutes,
                    CoverImageUrl = row.CoverImageUrl
                });
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

        /// <summary>
        /// AppId形式が有効なものだけを対象に、Steam appdetails API から
        /// カバー画像URLを同時実行数を制限して解決する。個々の失敗は無視して null に留め、
        /// 一括インポート全体を失敗させない。
        /// </summary>
        private async Task<Dictionary<string, string?>> ResolveCoverImageUrlsAsync(
            IEnumerable<string> appIds)
        {
            var targetAppIds = appIds
                .Where(IsValidAppIdFormat)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var results = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase);
            if (targetAppIds.Count == 0) return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            using var semaphore = new SemaphoreSlim(MaxConcurrentCoverLookups);
            var tasks = targetAppIds.Select(async appId =>
            {
                await semaphore.WaitAsync();
                try
                {
                    results[appId] = await _coverLookup.GetHeaderImageUrlAsync(appId);
                }
                catch (Exception ex)
                {
                    results[appId] = null;
                    _logger.LogWarning(ex, "Steam cover image lookup failed for AppId {AppId}.", appId);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return new Dictionary<string, string?>(results, StringComparer.OrdinalIgnoreCase);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestFormLimits(ValueCountLimit = MaxGameCount + 2)]
        public async Task<IActionResult> Confirm(SteamImportPreviewViewModel model)
        {
            var result = new SteamImportResultViewModel();
            string tempPath;
            try
            {
                tempPath = ResolveTempPath(model.TempFilePath);
                if (HttpContext.Session.GetString(GetSessionKey(model.TempFilePath!)) != "owned")
                    throw new InvalidOperationException("Import token is not owned by this session.");
            }
            catch (InvalidOperationException)
            {
                result.Errors.Add("確認データが無効です。もう一度アップロードしてください。");
                return View("Result", result);
            }

            if (!System.IO.File.Exists(tempPath))
            {
                HttpContext.Session.Remove(GetSessionKey(model.TempFilePath!));
                result.Errors.Add("確認データの有効期限が切れました。もう一度アップロードしてください。");
                return View("Result", result);
            }

            var selectedAppIds = model.SelectedAppIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxGameCount)
                .ToList();

            try
            {
                await using var input = System.IO.File.OpenRead(tempPath);
                var snapshot = await JsonSerializer.DeserializeAsync<SteamImportSnapshot>(input)
                    ?? new SteamImportSnapshot();
                var allowedRows = snapshot.Rows
                    .Where(x => selectedAppIds.Contains(x.AppId))
                    .GroupBy(x => x.AppId, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
                var allowedAppIds = allowedRows.Select(x => x.AppId).ToList();
                var existingAppIds = _repository.GetBaseQuery()
                    .Where(x => x.SteamAppId != null && allowedAppIds.Contains(x.SteamAppId))
                    .Select(x => x.SteamAppId!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                result.SkippedCount += selectedAppIds.Count - allowedRows.Count;
                foreach (var row in allowedRows)
                {
                    if (GetValidationError(row) != null || existingAppIds.Contains(row.AppId))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    try
                    {
                        _repository.Insert(ToEntity(row));
                        existingAppIds.Add(row.AppId);
                        result.ImportedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Steam item import failed for AppId {AppId}.", row.AppId);
                        result.Errors.Add($"Steam App ID「{row.AppId}」を登録できませんでした。");
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Steam import snapshot was invalid.");
                result.Errors.Add("確認データを読み込めませんでした。もう一度アップロードしてください。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Steam import confirmation failed.");
                result.Errors.Add("Steam一括登録中にエラーが発生しました。");
            }
            finally
            {
                DeleteTempFile(tempPath);
                HttpContext.Session.Remove(GetSessionKey(model.TempFilePath!));
            }

            return View("Result", result);
        }

        internal static SteamImportParseResult ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("ファイルが空です。");

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("JSON の形式が正しくありません。");
                if (!root.TryGetProperty("games", out var games) ||
                    games.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("JSON に games 配列がありません。");
                }

                var result = new SteamImportParseResult
                {
                    SteamId = ReadString(root, "steamId")
                };

                if (TryReadInt(root, "gameCount", out var gameCount))
                    result.TotalGames = gameCount;

                foreach (var game in games.EnumerateArray())
                {
                    if (game.ValueKind != JsonValueKind.Object ||
                        !TryReadAppId(game, out var appId) ||
                        string.IsNullOrWhiteSpace(ReadString(game, "name")))
                    {
                        throw new InvalidDataException("appid と name は各項目で必須です。");
                    }

                    TryReadInt(game, "playtime_forever", out var playtimeMinutes);
                    result.Rows.Add(new SteamImportSnapshotRow
                    {
                        AppId = appId,
                        Name = ReadString(game, "name")!.Trim(),
                        PlaytimeMinutes = Math.Max(0, playtimeMinutes),
                        CoverImageUrl = ReadCoverImageUrl(game)
                    });
                }

                if (result.Rows.Count == 0)
                    throw new InvalidDataException("登録対象のデータがありません。");

                if (result.TotalGames == 0)
                    result.TotalGames = result.Rows.Count;

                return result;
            }
            catch (JsonException)
            {
                throw new InvalidDataException("JSON の形式が正しくありません。");
            }
        }

        internal static ItemEntity ToEntity(SteamImportSnapshotRow row)
        {
            return new ItemEntity
            {
                SteamAppId = row.AppId,
                Title = row.Name,
                MediaType = MediaType.Game,
                Platform = "PC (Steam)",
                IsDigital = true,
                OwnershipStatus = OwnershipStatus.Owned,
                AcquisitionDate = DateTime.Today,
                CoverImageUrl = row.CoverImageUrl,
                Memo = BuildMemo(row.PlaytimeMinutes)
            };
        }

        internal static bool IsValidAppIdFormat(string appId)
        {
            return !string.IsNullOrWhiteSpace(appId) &&
                   appId.Length <= 20 &&
                   appId.All(char.IsDigit);
        }

        internal static string? SelectCoverImageUrl(
            SteamImportRow row,
            IReadOnlyDictionary<string, string?> resolvedCoverImageUrls)
        {
            return row.CoverImageUrl
                   ?? resolvedCoverImageUrls.GetValueOrDefault(row.AppId);
        }

        internal static string? BuildMemo(int playtimeMinutes)
        {
            if (playtimeMinutes <= 0) return null;
            return string.Format(
                CultureInfo.InvariantCulture,
                "Steam プレイ時間: {0:F1} 時間",
                playtimeMinutes / 60.0);
        }

        internal static string? GetValidationError(SteamImportSnapshotRow row)
        {
            if (string.IsNullOrWhiteSpace(row.AppId))
                return "Steam App IDを取得できません。";
            if (row.AppId.Length > 20)
                return "Steam App IDが登録可能な長さを超えています。";
            if (row.AppId.Any(x => !char.IsDigit(x)))
                return "Steam App IDの形式が正しくありません。";
            if (string.IsNullOrWhiteSpace(row.Name))
                return "タイトルを取得できません。";
            if (row.Name.Length > 500)
                return "タイトルが登録可能な長さを超えています。";
            if (row.CoverImageUrl is { Length: > 1000 })
                return "カバー画像URLが登録可能な長さを超えています。";
            return null;
        }

        internal static void MarkDuplicates(List<SteamImportRow> rows, ISet<string> existingAppIds)
        {
            var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Where(x => x.Error == null))
            {
                if (existingAppIds.Contains(row.AppId) || !seenAppIds.Add(row.AppId))
                    row.IsDuplicate = true;
            }
        }

        internal static async Task<string> ReadSteamJsonFileAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".json")
                throw new InvalidDataException("JSONファイルを選択してください。");
            if (file.Length == 0)
                throw new InvalidDataException("ファイルが空です。");
            if (file.Length > MaxFileSize)
                throw new InvalidDataException("Steam JSONファイルは5MB以下にしてください。");

            await using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
            return await reader.ReadToEndAsync();
        }

        private static bool CanImport(SteamImportRow row)
        {
            return !row.IsDuplicate && row.Error == null;
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null
            };
        }

        private static string? ReadCoverImageUrl(JsonElement element)
        {
            foreach (var propertyName in new[]
                     {
                         "coverImageUrl",
                         "headerImageUrl",
                         "imageUrl",
                         "capsuleImageUrl",
                         "libraryCapsuleUrl",
                         "thumbnailUrl",
                         "header_image",
                         "cover"
                     })
            {
                var url = NormalizeCoverImageUrl(ReadString(element, propertyName));
                if (url != null) return url;
            }

            return null;
        }

        private static string? NormalizeCoverImageUrl(string? value)
        {
            var url = value?.Trim();
            if (string.IsNullOrEmpty(url)) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            return uri.Scheme is "http" or "https" ? url : null;
        }

        private static bool TryReadAppId(JsonElement element, out string appId)
        {
            appId = ReadString(element, "appid")?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(appId);
        }

        private static bool TryReadInt(JsonElement element, string propertyName, out int value)
        {
            value = 0;
            if (!element.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
                return true;
            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            return false;
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

        private static string GetSessionKey(string token) => $"SteamImport:{token}";
    }

    internal class SteamImportParseResult
    {
        public string? SteamId { get; set; }
        public int TotalGames { get; set; }
        public List<SteamImportSnapshotRow> Rows { get; set; } = new();
    }
}
