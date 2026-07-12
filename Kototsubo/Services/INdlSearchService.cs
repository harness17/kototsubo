namespace Site.Services
{
    public interface INdlSearchService
    {
        /// <summary>
        /// 国立国会図書館サーチ API で書誌を検索する。
        /// </summary>
        /// <param name="criteria">検索条件（ISBN・タイトル・著者・出版社・出版年範囲）。</param>
        /// <param name="startRecord">取得開始位置（1 始まり）。ページングに使う。</param>
        /// <param name="maxRecords">1 ページあたりの取得件数。</param>
        Task<NdlSearchResponse> SearchAsync(
            NdlSearchCriteria criteria, int startRecord = 1, int maxRecords = 20);
    }

    /// <summary>
    /// タイトル検索の検索条件。指定された項目のみ CQL クエリに AND 結合される。
    /// </summary>
    public class NdlSearchCriteria
    {
        public string? ISBN { get; set; }
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public NdlSearchSortOrder SortOrder { get; set; }

        /// <summary>出版年（開始）。NDL の from キーに対応。</summary>
        public int? YearFrom { get; set; }

        /// <summary>出版年（終了）。NDL の until キーに対応。</summary>
        public int? YearTo { get; set; }

        /// <summary>検索条件が 1 つも指定されていないか。</summary>
        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(ISBN) &&
            string.IsNullOrWhiteSpace(Title) &&
            string.IsNullOrWhiteSpace(Creator) &&
            string.IsNullOrWhiteSpace(Publisher) &&
            YearFrom == null && YearTo == null;
    }

    public enum NdlSearchSortOrder
    {
        Default = 0,
        PublicationDateDescending = 1,
        PublicationDateAscending = 2
    }

    public class NdlSearchResponse
    {
        public bool Succeeded { get; set; } = true;
        public int TotalResults { get; set; }
        public bool IsTruncated { get; set; }
        public List<NdlSearchResult> Results { get; set; } = new();
    }

    public class NdlSearchResult
    {
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public string? ISBN { get; set; }
        public string? PublicationDate { get; set; }
    }
}
