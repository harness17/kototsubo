using System.ComponentModel.DataAnnotations;

namespace Site.Models
{
    /// <summary>Steam ゲームリスト JSON の一括登録入力フォーム。</summary>
    public class SteamImportViewModel
    {
        [Display(Name = "Steam ゲームリスト JSON")]
        public IFormFile? File { get; set; }
    }

    /// <summary>Steam ゲームリスト一括登録プレビュー。</summary>
    public class SteamImportPreviewViewModel
    {
        public List<SteamImportRow> Rows { get; set; } = new();
        public List<string> SelectedAppIds { get; set; } = new();
        public string? TempFilePath { get; set; }
        public int TotalGames { get; set; }
        public string? SteamId { get; set; }
    }

    /// <summary>Steam プレビュー 1 行。</summary>
    public class SteamImportRow
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int PlaytimeMinutes { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsDuplicate { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>確定時に参照する Steam 一時スナップショット。</summary>
    public class SteamImportSnapshot
    {
        public List<SteamImportSnapshotRow> Rows { get; set; } = new();
    }

    /// <summary>Steam スナップショット 1 行。</summary>
    public class SteamImportSnapshotRow
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int PlaytimeMinutes { get; set; }

        /// <summary>
        /// プレビュー時点で Steam appdetails API から解決したカバー画像URL。
        /// 確定登録時はこの値をそのまま使い、API を再度呼び出さない。
        /// </summary>
        public string? CoverImageUrl { get; set; }
    }

    /// <summary>Steam 一括登録結果。</summary>
    public class SteamImportResultViewModel
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
