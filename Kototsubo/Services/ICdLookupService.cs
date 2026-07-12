namespace Site.Services
{
    /// <summary>
    /// 音楽 CD を JAN コードから検索する専門サービス。
    /// 楽天ブックス CD 検索 API（BooksCD/Search）の呼び出しを担う。
    /// 現在はアプリ ID 取得前のためスタブ実装（<see cref="CdLookupServiceStub"/>）を使用する。
    /// </summary>
    public interface ICdLookupService
    {
        /// <summary>JAN コード群から音楽 CD の書誌を一括検索する。</summary>
        /// <param name="jans">検索対象の JAN（生文字列。正規化は実装側で行う）。</param>
        /// <returns>入力と同じ順序の検索結果。見つからない・無効な要素は null。</returns>
        Task<IReadOnlyList<CdLookupResult?>> LookupByJansAsync(IReadOnlyList<string> jans);
    }

    /// <summary>
    /// 音楽 CD の書誌検索結果。CD 専門のフィールド構成を持つ
    /// （メディア種別は呼び出し側で <c>MediaType.Music</c> として確定する）。
    /// </summary>
    public class CdLookupResult
    {
        /// <summary>タイトル。</summary>
        public string? Title { get; set; }

        /// <summary>アーティスト（楽天 artistName 相当）。</summary>
        public string? Creator { get; set; }

        /// <summary>レーベル（楽天 label 相当）。</summary>
        public string? Publisher { get; set; }

        /// <summary>発売日（楽天 salesDate 相当）。</summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>書影 URL（楽天 largeImageUrl 相当）。</summary>
        public string? CoverImageUrl { get; set; }

        /// <summary>正規化済み 13 桁 JAN。</summary>
        public string? Jan { get; set; }

        /// <summary>ディスク枚数（楽天 discNumber 相当）。</summary>
        public int? DiscCount { get; set; }
    }
}
