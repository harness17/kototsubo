using System.ComponentModel.DataAnnotations;
using Dev.CommonLibrary.Common;
using Site.Services;

namespace Site.Models
{
    public class KindleImportViewModel
    {
        [Required(ErrorMessage = "ファイルを選択してください。")]
        public IFormFile? File { get; set; }
    }

    public class KindleImportPreviewViewModel
    {
        public List<KindleImportRow> Rows { get; set; } = new();
        public int SkipCount { get; set; }
        public int EnrichmentFailedCount { get; set; }
        public string? TempFilePath { get; set; }
        public List<string> SelectedAsins { get; set; } = new();
    }

    public class KindleImportRow
    {
        public string? ASIN { get; set; }
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Series { get; set; }
        public int? Volume { get; set; }
        public DateTime? AcquiredTime { get; set; }
        public string? ReadStatus { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool IsDuplicate { get; set; }

        // openBD 逆引き補完フィールド
        public string? ISBN { get; set; }
        public string? Publisher { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public int? PageCount { get; set; }

        /// <summary>NDL一時失敗で書誌補完が完了しなかった行。CSV由来の基本情報は保持したまま登録可能。</summary>
        public bool EnrichmentFailed { get; set; }
    }

    public class KindleImportSnapshot
    {
        public List<KindleImportRow> Rows { get; set; } = new();
    }

    public class KindleImportResultViewModel
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class IsbnImportViewModel
    {
        public const int MaxInputLength = 400000;

        [StringLength(MaxInputLength, ErrorMessage = "ISBN入力が長すぎます。")]
        [Display(Name = "ISBN")]
        public string? Isbns { get; set; }

        [Display(Name = "ISBNファイル")]
        public IFormFile? File { get; set; }
    }

    public class IsbnImportPreviewViewModel
    {
        public List<IsbnImportRow> Rows { get; set; } = new();

        /// <summary>
        /// 登録候補の一意キー。ISBN取得できた行はISBN、ISBNなしのタイトル検索選択行は
        /// 合成キーを保持する（<see cref="IsbnImportSnapshotRow.Key"/> と対応）。
        /// </summary>
        public List<string> SelectedKeys { get; set; } = new();
        public Dictionary<string, int> SelectedCandidateIndexes { get; set; } = new();
        public string? TempFilePath { get; set; }

        /// <summary>
        /// プレビューの「戻る」リンク先アクション名。
        /// 手入力フローは "Isbn"、タイトル検索フローは "TitleSearch"。
        /// 登録フロー共通のプレビュー画面を複数の入口から再利用するための分岐。
        /// </summary>
        public string BackAction { get; set; } = "Isbn";

        /// <summary>
        /// 「戻る」リンク先 URL（検索条件・ページ番号を含む）。
        /// 設定されている場合は <see cref="BackAction"/> より優先する。
        /// タイトル検索フローで、戻った先に検索条件と結果を再現するために使う。
        /// </summary>
        public string? BackUrl { get; set; }
    }

    public class IsbnImportRow
    {
        public string Input { get; set; } = string.Empty;

        /// <summary>登録候補の一意キー。<see cref="IsbnImportSnapshotRow.Key"/> と対応する。</summary>
        public string Key { get; set; } = string.Empty;
        public BookLookupResult? Book { get; set; }
        public List<BookLookupResult> Candidates { get; set; } = new();
        public bool IsDuplicate { get; set; }
        public string? Error { get; set; }
        public string? AmazonAsinCandidate { get; set; }
        public bool HasLocalAmazonMatch { get; set; }

        /// <summary>NDL一時障害等で書誌取得に失敗した行。再取得ボタンの表示判定に使う。</summary>
        public bool NdlLookupFailed { get; set; }
    }

    public class IsbnImportResultViewModel
    {
        public int ImportedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public string ContinueAction { get; set; } = "Isbn";
    }

    public class IsbnImportSnapshot
    {
        public List<IsbnImportSnapshotRow> Rows { get; set; } = new();
        public string SourceAction { get; set; } = "Isbn";

        /// <summary>NDL取得に失敗した入力ISBN。再取得時にこのリストだけを再照会する。</summary>
        public List<string> FailedInputs { get; set; } = new();

        /// <summary>タイトル検索フローの「戻る」URL。再取得後も保持するためスナップショットに持つ。</summary>
        public string? BackUrl { get; set; }
    }

    public class IsbnImportSnapshotRow
    {
        /// <summary>
        /// 登録候補の一意キー。ISBN取得できた行はISBN、ISBNなしのタイトル検索選択行は
        /// <see cref="ImportController.BuildIsbnPreviewAsync"/> が発行する合成キーを保持する。
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>実際のISBN。タイトル検索で選択したISBNなし書籍はnull。</summary>
        public string? ISBN { get; set; }
        public List<BookLookupResult> Candidates { get; set; } = new();
        public string? AmazonAsinCandidate { get; set; }
    }

    public class TitleSearchSelectionInput
    {
        public string? ISBN { get; set; }
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public string? PublicationDate { get; set; }
    }

    /// <summary>
    /// ISBNなしのタイトル検索選択行をクライアントから受け取るための入力。
    /// ISBNがなく外部再照会できないため、検索時点の書誌情報をそのまま登録候補として使う。
    /// </summary>
    public class NoIsbnSelectionInput
    {
        public string? Key { get; set; }
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public string? PublicationDate { get; set; }
    }

    public class TitleSearchViewModel : IValidatableObject
    {
        [StringLength(200, ErrorMessage = "タイトルは200文字以内で入力してください。")]
        [Display(Name = "タイトル")]
        public string? Title { get; set; }

        [StringLength(200, ErrorMessage = "著者名は200文字以内で入力してください。")]
        [Display(Name = "著者")]
        public string? Creator { get; set; }

        [StringLength(200, ErrorMessage = "出版社名は200文字以内で入力してください。")]
        [Display(Name = "出版社")]
        public string? Publisher { get; set; }

        [Range(1, 9999, ErrorMessage = "出版年は1〜9999で入力してください。")]
        [Display(Name = "出版年（開始）")]
        public int? YearFrom { get; set; }

        [Range(1, 9999, ErrorMessage = "出版年は1〜9999で入力してください。")]
        [Display(Name = "出版年（終了）")]
        public int? YearTo { get; set; }

        [Display(Name = "並び順")]
        public NdlSearchSortOrder SortOrder { get; set; } = NdlSearchSortOrder.PublicationDateDescending;

        public List<TitleSearchResultRow> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public bool ResultsLimited { get; set; }
        public bool SearchFailed { get; set; }
        public bool HasSearched { get; set; }

        /// <summary>
        /// 検索が実行されたか（検索フォームから送信されたか）。
        /// 初回アクセス（クエリなし）と「空のまま検索実行」を区別するために使う。
        /// </summary>
        public bool Searched { get; set; }

        /// <summary>現在のページ番号（1 始まり）。</summary>
        public int Page { get; set; } = 1;

        /// <summary>ページャ表示用サマリー（総件数・表示範囲）。</summary>
        public CommonListSummaryModel? Summary { get; set; }

        /// <summary>検索条件が 1 つ以上指定されているか。</summary>
        public bool HasAnyCriteria =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Creator) ||
            !string.IsNullOrWhiteSpace(Publisher) ||
            YearFrom != null || YearTo != null;

        /// <summary>検索条件を NDL 検索用の条件オブジェクトに変換する。</summary>
        public NdlSearchCriteria ToCriteria() => new()
        {
            Title = Title,
            Creator = Creator,
            Publisher = Publisher,
            YearFrom = YearFrom,
            YearTo = YearTo,
            SortOrder = SortOrder
        };

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // 初回表示では検索条件を検証しない。
            // 検索フォームから送信された場合だけ、空条件や年範囲をエラーにする。
            if (!Searched)
                yield break;

            if (!HasAnyCriteria)
            {
                yield return new ValidationResult(
                    "検索条件を1つ以上指定してください。",
                    new[] { nameof(Title) });
            }

            if (YearFrom.HasValue && YearTo.HasValue && YearFrom > YearTo)
            {
                yield return new ValidationResult(
                    "出版年の範囲が正しくありません（開始 ≤ 終了）。",
                    new[] { nameof(YearTo) });
            }
        }
    }

    public class TitleSearchResultRow
    {
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public string? ISBN { get; set; }
        public string? PublicationDate { get; set; }

        /// <summary>
        /// この本の ISBN が既に蔵品として登録済みかどうか。
        /// true の場合、検索結果一覧で非活性（選択不可）表示にする。
        /// </summary>
        public bool IsAlreadyRegistered { get; set; }
    }
}
