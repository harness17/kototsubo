using System.ComponentModel.DataAnnotations;
using Site.Services;

namespace Site.Models
{
    /// <summary>DVD/Blu-ray の JAN 一括登録入力フォーム。</summary>
    public class DvdImportViewModel
    {
        public const int MaxInputLength = 400000;

        [StringLength(MaxInputLength, ErrorMessage = "JAN入力が長すぎます。")]
        [Display(Name = "JANコード")]
        public string? Jans { get; set; }

        [Display(Name = "JANファイル")]
        public IFormFile? File { get; set; }
    }

    public class DvdImportPreviewViewModel
    {
        public List<DvdImportRow> Rows { get; set; } = new();
        public List<string> SelectedJans { get; set; } = new();
        public string? TempFilePath { get; set; }
    }

    public class DvdImportRow
    {
        public string Input { get; set; } = string.Empty;
        public DvdLookupResult? Result { get; set; }
        public bool IsDuplicate { get; set; }
        public string? Error { get; set; }
    }

    public class DvdImportSnapshot
    {
        public List<DvdImportSnapshotRow> Rows { get; set; } = new();
    }

    public class DvdImportSnapshotRow
    {
        public DvdLookupResult Result { get; set; } = new();
    }

    public class DvdImportResultViewModel
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
