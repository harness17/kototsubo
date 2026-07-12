using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace Dev.CommonLibrary.Pdf;

/// <summary>
/// <see cref="PlaywrightPdfService.GenerateFromUrlAsync"/> へ渡す
/// ループバック URL と認証クッキーを安全に構築するヘルパー。
/// </summary>
/// <remarks>
/// Host ヘッダーは外部から改ざん可能なため信頼せず、同一プロセスのループバック宛先へ固定する。
/// 認証クッキーは許可プレフィックスのものだけに絞り、不要なクッキーを内部レンダリングへ渡さない。
/// </remarks>
public static class PdfPrintHelper
{
    /// <summary>ループバック宛先のホスト。</summary>
    public const string LoopbackHost = "127.0.0.1";

    /// <summary>PDF レンダリングへ渡してよい既定の認証クッキープレフィックス。</summary>
    public static readonly string[] DefaultAuthCookiePrefixes =
    {
        ".AspNetCore.Identity.Application",
        ".AspNetCore.Session"
    };

    /// <summary>
    /// Host ヘッダーを信頼せず、同一プロセスのループバック URL を組み立てる。
    /// IIS in-process や PathBase 付きのサブアプリ配置でも壊れないよう、現リクエストから構築する。
    /// </summary>
    /// <param name="request">現在のリクエスト</param>
    /// <param name="pathAndQuery">PathBase より後ろのパス＋クエリ（先頭スラッシュ込み、例: <c>/Statistics?print=1</c>）</param>
    /// <remarks>
    /// スキームとポートは Host ヘッダー（改ざん可能）ではなく実接続から取得する。
    /// スキームは <see cref="HttpRequest.Scheme"/>、ポートは <see cref="ConnectionInfo.LocalPort"/> を優先し、
    /// HTTPS で着信したリクエストに対して HTTP ポートへ接続して失敗する事故を防ぐ。
    /// </remarks>
    public static string BuildLoopbackUrl(HttpRequest request, string pathAndQuery)
    {
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
        var scheme = string.IsNullOrEmpty(request.Scheme) ? "https" : request.Scheme;

        // 実接続ポートを優先。取得できない場合のみ Host のポート、最後にスキーム既定ポートへフォールバック。
        var port = request.HttpContext.Connection.LocalPort;
        if (port <= 0)
        {
            port = request.Host.Port ?? (scheme == "https" ? 443 : 80);
        }

        return $"{scheme}://{LoopbackHost}:{port}{pathBase}{pathAndQuery}";
    }

    /// <summary>
    /// 許可プレフィックスに一致する認証クッキーだけを Playwright 用クッキーへ変換する。
    /// ループバック http アクセスのため Secure=false で渡す。
    /// </summary>
    /// <param name="request">現在のリクエスト</param>
    /// <param name="allowedCookiePrefixes">許可するクッキー名のプレフィックス。null の場合は <see cref="DefaultAuthCookiePrefixes"/></param>
    public static List<Cookie> ExtractAuthCookies(
        HttpRequest request,
        IEnumerable<string>? allowedCookiePrefixes = null)
    {
        var prefixes = allowedCookiePrefixes?.ToArray() ?? DefaultAuthCookiePrefixes;

        return request.Cookies
            .Where(c => prefixes.Any(prefix => c.Key.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(c => new Cookie
            {
                Name = c.Key,
                Value = c.Value,
                Domain = LoopbackHost,
                Path = "/",
                Secure = false,
                HttpOnly = true
            })
            .ToList();
    }
}
