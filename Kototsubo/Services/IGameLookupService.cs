namespace Site.Services
{
    /// <summary>
    /// ゲームソフトを JAN コードから検索する専門サービス。
    /// 楽天ブックスゲーム検索 API（BooksGame/Search）の呼び出しを担う。
    /// 現在はアプリ ID 取得前のためスタブ実装（<see cref="GameLookupServiceStub"/>）を使用する。
    /// </summary>
    public interface IGameLookupService
    {
        /// <summary>JAN コード群からゲームソフトの書誌を一括検索する。</summary>
        /// <param name="jans">検索対象の JAN（生文字列。正規化は実装側で行う）。</param>
        /// <returns>入力と同じ順序の検索結果。見つからない・無効な要素は null。</returns>
        Task<IReadOnlyList<GameLookupResult?>> LookupByJansAsync(IReadOnlyList<string> jans);
    }

    /// <summary>
    /// ゲームソフトの書誌検索結果。ゲーム専門のフィールド構成を持つ
    /// （メディア種別は呼び出し側で <c>MediaType.Game</c> として確定する）。
    /// </summary>
    public class GameLookupResult
    {
        /// <summary>タイトル。</summary>
        public string? Title { get; set; }

        /// <summary>メーカー・開発元（楽天 label 相当）。</summary>
        public string? Publisher { get; set; }

        /// <summary>発売日（楽天 salesDate 相当）。</summary>
        public DateTime? ReleaseDate { get; set; }

        /// <summary>書影 URL（楽天 largeImageUrl 相当）。</summary>
        public string? CoverImageUrl { get; set; }

        /// <summary>正規化済み 13 桁 JAN。</summary>
        public string? Jan { get; set; }

        /// <summary>対応プラットフォーム（楽天 hardware 相当。例: PS5, Switch）。</summary>
        public string? Platform { get; set; }
    }
}
