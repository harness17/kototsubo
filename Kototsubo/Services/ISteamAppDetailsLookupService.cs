namespace Site.Services
{
    /// <summary>Steam Store の appdetails API からカバー画像URLを取得するサービス。</summary>
    public interface ISteamAppDetailsLookupService
    {
        /// <summary>
        /// 指定 App ID のヘッダー画像URLを取得する。
        /// 取得できない場合（404・タイムアウト・レート制限等）は null を返す。
        /// </summary>
        Task<string?> GetHeaderImageUrlAsync(string appId);
    }
}
