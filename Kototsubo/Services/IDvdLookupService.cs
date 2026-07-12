namespace Site.Services
{
    /// <summary>DVD/Blu-ray を JAN コードから検索する専門サービス。</summary>
    public interface IDvdLookupService
    {
        /// <summary>JAN コード群から映像作品の書誌を一括検索する。</summary>
        Task<IReadOnlyList<DvdLookupResult?>> LookupByJansAsync(IReadOnlyList<string> jans);
    }

    /// <summary>DVD/Blu-ray の書誌検索結果。</summary>
    public class DvdLookupResult
    {
        public string? Title { get; set; }
        public string? Creator { get; set; }
        public string? Publisher { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? Jan { get; set; }
        public string? Format { get; set; }
        public int? DiscCount { get; set; }
    }
}
