using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace Dev.CommonLibrary.Extensions.Helper
{
    /// <summary>
    /// HTML ヘルパー拡張で生成した IHtmlContent を文字列化するための内部ユーティリティ。
    /// </summary>
    internal static class HtmlContentExtensions
    {
        /// <summary>
        /// IHtmlContent を HTML としてレンダリングした文字列を返す。
        /// </summary>
        internal static string ToHtmlString(this IHtmlContent content)
        {
            using var writer = new StringWriter();
            content.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
        }
    }
}
