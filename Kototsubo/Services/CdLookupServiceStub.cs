namespace Site.Services
{
    /// <summary>
    /// <see cref="ICdLookupService"/> のスタブ実装。
    /// 楽天 API のアプリ ID 取得前に、登録フロー（入力→プレビュー→確定）の
    /// 疎通確認を行うため、正規化できた JAN に対して固定的なダミー書誌を返す。
    /// JAN 正規化に失敗した入力は null（＝プレビューで行エラー＝スキップ）とする。
    /// 実 API 実装（楽天 BooksCD/Search 呼び出し）取得後に本クラスを差し替える。
    /// </summary>
    public class CdLookupServiceStub : ICdLookupService
    {
        public Task<IReadOnlyList<CdLookupResult?>> LookupByJansAsync(
            IReadOnlyList<string> jans)
        {
            var results = new List<CdLookupResult?>(jans.Count);
            for (var i = 0; i < jans.Count; i++)
            {
                var jan = JanCode.Normalize(jans[i]);
                if (jan == null)
                {
                    results.Add(null);
                    continue;
                }

                results.Add(new CdLookupResult
                {
                    Title = $"[CDスタブ] サンプルアルバム {jan}",
                    Creator = "サンプルアーティスト",
                    Publisher = "サンプルレーベル",
                    ReleaseDate = new DateTime(2021, 5, 1),
                    CoverImageUrl = null,
                    Jan = jan,
                    DiscCount = 1
                });
            }

            return Task.FromResult<IReadOnlyList<CdLookupResult?>>(results);
        }
    }
}
