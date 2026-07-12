namespace Site.Services
{
    /// <summary>
    /// <see cref="IGameLookupService"/> のスタブ実装。
    /// 楽天 API のアプリ ID 取得前に、登録フロー（入力→プレビュー→確定）の
    /// 疎通確認を行うため、正規化できた JAN に対して固定的なダミー書誌を返す。
    /// JAN 正規化に失敗した入力は null（＝プレビューで行エラー＝スキップ）とする。
    /// 実 API 実装（楽天 BooksGame/Search 呼び出し）取得後に本クラスを差し替える。
    /// </summary>
    public class GameLookupServiceStub : IGameLookupService
    {
        public Task<IReadOnlyList<GameLookupResult?>> LookupByJansAsync(
            IReadOnlyList<string> jans)
        {
            var results = new List<GameLookupResult?>(jans.Count);
            for (var i = 0; i < jans.Count; i++)
            {
                var jan = JanCode.Normalize(jans[i]);
                if (jan == null)
                {
                    results.Add(null);
                    continue;
                }

                results.Add(new GameLookupResult
                {
                    Title = $"[ゲームスタブ] サンプルゲーム {jan}",
                    Publisher = "サンプルメーカー",
                    ReleaseDate = new DateTime(2022, 9, 1),
                    CoverImageUrl = null,
                    Jan = jan,
                    // 主要プラットフォームを疎通確認できるよう交互に割り当てる
                    Platform = (i % 2 == 0) ? "Nintendo Switch" : "PlayStation 5"
                });
            }

            return Task.FromResult<IReadOnlyList<GameLookupResult?>>(results);
        }
    }
}
