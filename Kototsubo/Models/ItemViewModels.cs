using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Common;
using Site.Common;

namespace Site.Models
{
    /// <summary>
    /// 所蔵品一覧画面のビューモデル。
    /// </summary>
    public class ItemListViewModel
    {
        public List<ItemViewModel> Items { get; set; } = new();
        public ItemSearchViewModel Search { get; set; } = new();
        public CommonListPagerModel Pager { get; set; } = new();
        public CommonListSummaryModel? Summary { get; set; }
    }

    /// <summary>
    /// 所蔵品一覧の検索条件ビューモデル。
    /// </summary>
    public class ItemSearchViewModel : IValidatableObject
    {
        [Display(Name = "キーワード")]
        public string? Keyword { get; set; }

        [Display(Name = "メディア種別")]
        public MediaType? MediaType { get; set; }

        [Display(Name = "版種")]
        public bool? IsDigital { get; set; }

        [Display(Name = "所有ステータス")]
        public OwnershipStatus? OwnershipStatus { get; set; }

        [Display(Name = "出版社")]
        [StringLength(500)]
        public string? Publisher { get; set; }

        [Display(Name = "発売日（開始）")]
        [DataType(DataType.Date)]
        public DateTime? ReleaseDateFrom { get; set; }

        [Display(Name = "発売日（終了）")]
        [DataType(DataType.Date)]
        public DateTime? ReleaseDateTo { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ReleaseDateFrom.HasValue &&
                ReleaseDateTo.HasValue &&
                ReleaseDateFrom.Value.Date > ReleaseDateTo.Value.Date)
            {
                yield return new ValidationResult(
                    "発売日の開始日は終了日以前を指定してください。",
                    [nameof(ReleaseDateFrom), nameof(ReleaseDateTo)]);
            }
        }
    }

    /// <summary>
    /// 所蔵品の表示・編集用ビューモデル。
    /// </summary>
    public class ItemViewModel
    {
        public long Id { get; set; }

        // === 共通 ===

        [Required(ErrorMessage = "タイトルは必須です")]
        [StringLength(500)]
        [Display(Name = "タイトル")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "メディア種別は必須です")]
        [Display(Name = "メディア種別")]
        public MediaType MediaType { get; set; }

        [Display(Name = "所有ステータス")]
        public OwnershipStatus OwnershipStatus { get; set; } = OwnershipStatus.Owned;

        [Display(Name = "デジタル版")]
        public bool IsDigital { get; set; }

        [Display(Name = "取得日")]
        [DataType(DataType.Date)]
        public DateTime? AcquisitionDate { get; set; }

        [Display(Name = "評価")]
        [Range(1, 5, ErrorMessage = "評価は1〜5で入力してください")]
        public int? Rating { get; set; }

        [Display(Name = "メモ")]
        [StringLength(2000)]
        public string? Memo { get; set; }

        [Display(Name = "カバー画像URL")]
        [StringLength(1000)]
        public string? CoverImageUrl { get; set; }

        // === 識別コード ===

        [Display(Name = "ISBN")]
        [StringLength(13)]
        public string? ISBN { get; set; }

        [Display(Name = "JANコード")]
        [StringLength(13)]
        public string? JANCode { get; set; }

        [Display(Name = "ASIN")]
        [StringLength(10)]
        public string? ASIN { get; set; }

        [Display(Name = "Steam App ID")]
        [StringLength(20)]
        public string? SteamAppId { get; set; }

        // === クリエイター・出版 ===

        [Display(Name = "クリエイター")]
        [StringLength(500)]
        public string? Creator { get; set; }

        [Display(Name = "出版社")]
        [StringLength(500)]
        public string? Publisher { get; set; }

        [Display(Name = "発売日")]
        [DataType(DataType.Date)]
        public DateTime? ReleaseDate { get; set; }

        // === メディア固有 ===

        [Display(Name = "プラットフォーム")]
        [StringLength(100)]
        public string? Platform { get; set; }

        [Display(Name = "フォーマット")]
        [StringLength(100)]
        public string? Format { get; set; }

        [Display(Name = "ページ数")]
        public int? PageCount { get; set; }

        [Display(Name = "ディスク枚数")]
        public int? DiscCount { get; set; }

        [Display(Name = "上映時間（分）")]
        public int? Runtime { get; set; }

        // === 監査（表示用） ===

        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}
