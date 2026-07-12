using System.ComponentModel.DataAnnotations;
using Site.Services;

namespace Site.Models
{
    /// <summary>ゲームソフトの JAN 一括登録入力フォーム。</summary>
    public class GameImportViewModel
    {
        public const int MaxInputLength = 400000;

        [StringLength(MaxInputLength, ErrorMessage = "JAN入力が長すぎます。")]
        [Display(Name = "JANコード")]
        public string? Jans { get; set; }

        [Display(Name = "JANファイル")]
        public IFormFile? File { get; set; }
    }

    /// <summary>ゲームソフト一括登録プレビュー。</summary>
    public class GameImportPreviewViewModel
    {
        public List<GameImportRow> Rows { get; set; } = new();
        public List<string> SelectedJans { get; set; } = new();
        public string? TempFilePath { get; set; }
    }

    /// <summary>プレビュー 1 行（入力 JAN と検索結果・状態）。</summary>
    public class GameImportRow
    {
        public string Input { get; set; } = string.Empty;
        public GameLookupResult? Result { get; set; }
        public bool IsDuplicate { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>確定時に参照する一時スナップショット。</summary>
    public class GameImportSnapshot
    {
        public List<GameImportSnapshotRow> Rows { get; set; } = new();
    }

    /// <summary>スナップショット 1 行。</summary>
    public class GameImportSnapshotRow
    {
        public GameLookupResult Result { get; set; } = new();
    }

    /// <summary>一括登録結果。</summary>
    public class GameImportResultViewModel
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
