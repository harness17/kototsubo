using Microsoft.AspNetCore.Http;

namespace Dev.CommonLibrary.Common
{
    /// <summary>
    /// Cookie操作Utility (ASP.NET Core版)
    /// </summary>
    public static class CookieUtility
    {
        /// <summary>指定キーの Cookie 値を返す。存在しない場合は null を返す。</summary>
        public static string? GetCookieValueByKey(IRequestCookieCollection cookies, string key)
        {
            cookies.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>Cookie を設定する。expires 省略時は 1 ヶ月後を有効期限とする。</summary>
        public static void SetCookie(IResponseCookies cookies, string key, string value, DateTimeOffset? expires = null)
        {
            cookies.Append(key, value, new CookieOptions
            {
                Path = "/",
                Expires = expires ?? DateTimeOffset.Now.AddMonths(1)
            });
        }

        /// <summary>Cookie を削除する（有効期限を過去に設定して上書き）。存在しないキーの場合は何もしない。</summary>
        public static void DeleteCookie(IRequestCookieCollection requestCookies, IResponseCookies responseCookies, string key)
        {
            if (!requestCookies.ContainsKey(key)) return;
            responseCookies.Append(key, "", new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.Now.AddMonths(-1)
            });
        }
    }
}
