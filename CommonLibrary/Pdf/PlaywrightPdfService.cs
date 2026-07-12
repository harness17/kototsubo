using Microsoft.Playwright;

namespace Dev.CommonLibrary.Pdf;

/// <summary>
/// HTML または内部 URL を Chromium の印刷機能で PDF に変換するサービス。
/// </summary>
/// <remarks>
/// <see cref="IPlaywright"/> は <see cref="IPlaywrightFactory"/> 経由で Singleton を使い回し、
/// Browser は per-request で起動する。
/// </remarks>
public class PlaywrightPdfService
{
    private readonly IPlaywrightFactory _playwrightFactory;

    public PlaywrightPdfService(IPlaywrightFactory playwrightFactory)
    {
        _playwrightFactory = playwrightFactory;
    }

    /// <summary>
    /// 印刷用 HTML を PDF バイト列に変換する。
    /// </summary>
    public async Task<byte[]> GenerateFromHtmlAsync(string html)
    {
        var playwright = await _playwrightFactory.GetAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            PreferCSSPageSize = true
        });
    }

    /// <summary>
    /// 内部 URL をレンダリングして PDF バイト列に変換する。
    /// 認証が必要なページや Chart.js などの動的描画を含むページを PDF 化する場合に使う。
    /// </summary>
    /// <param name="url">レンダリング対象のフル URL（ループバック URL を推奨）</param>
    /// <param name="cookies">認証用クッキー（<see cref="PdfPrintHelper"/> で構築可）。不要なら null</param>
    /// <param name="readyFlagJs">
    /// 描画完了判定の JS 式（true 評価で完了とみなす）。
    /// 例: <c>"() =&gt; window.chartsReady === true"</c>。null の場合は待機しない。
    /// </param>
    /// <param name="landscape">横向きで出力するか</param>
    /// <param name="timeoutMs">ページ取得・描画完了待機の各タイムアウト</param>
    public async Task<byte[]> GenerateFromUrlAsync(
        string url,
        IEnumerable<Cookie>? cookies = null,
        string? readyFlagJs = null,
        bool landscape = false,
        int timeoutMs = 30000)
    {
        var playwright = await _playwrightFactory.GetAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            // ループバック(127.0.0.1)宛ての HTTPS は開発用自己署名証明書になるため許可する。
            // 接続先がループバックに固定されているため証明書検証を省いても外部中間者リスクは生じない。
            IgnoreHTTPSErrors = true
        });

        if (cookies is not null)
        {
            await context.AddCookiesAsync(cookies);
        }

        var page = await context.NewPageAsync();
        try
        {
            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = timeoutMs
            });

            if (response is null || !response.Ok)
            {
                var status = response?.Status.ToString() ?? "no response";
                throw new InvalidOperationException($"印刷ページの取得に失敗しました (status={status})");
            }

            if (!string.IsNullOrEmpty(readyFlagJs))
            {
                await page.WaitForFunctionAsync(readyFlagJs, null, new PageWaitForFunctionOptions
                {
                    Timeout = timeoutMs
                });
            }

            return await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                Landscape = landscape,
                PrintBackground = true,
                Margin = new Margin { Top = "8mm", Bottom = "8mm", Left = "8mm", Right = "8mm" }
            });
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
