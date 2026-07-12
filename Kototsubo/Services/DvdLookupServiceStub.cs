namespace Site.Services
{
    /// <summary>正規化できた JAN に固定的なダミー映像書誌を返すスタブ。</summary>
    public class DvdLookupServiceStub : IDvdLookupService
    {
        public Task<IReadOnlyList<DvdLookupResult?>> LookupByJansAsync(
            IReadOnlyList<string> jans)
        {
            var results = new List<DvdLookupResult?>(jans.Count);
            for (var i = 0; i < jans.Count; i++)
            {
                var jan = JanCode.Normalize(jans[i]);
                if (jan == null)
                {
                    results.Add(null);
                    continue;
                }

                results.Add(new DvdLookupResult
                {
                    Title = $"[DVDスタブ] サンプル映像 {jan}",
                    Creator = "サンプル出演者・監督",
                    Publisher = "サンプルレーベル",
                    ReleaseDate = new DateTime(2020, 11, 1),
                    CoverImageUrl = null,
                    Jan = jan,
                    Format = i % 2 == 0 ? "DVD" : "Blu-ray",
                    DiscCount = 1
                });
            }

            return Task.FromResult<IReadOnlyList<DvdLookupResult?>>(results);
        }
    }
}
