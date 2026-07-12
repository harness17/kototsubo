using System.Web;

namespace Dev.CommonLibrary.Extensions
{
    /// <summary>string に対する汎用拡張メソッド群。</summary>
    public static class StringExtensions
    {
        /// <summary>HTML エンコードした文字列を返す。null の場合は null を返す。</summary>
        public static string? HtmlEncode(this string? value) => value == null ? null : HttpUtility.HtmlEncode(value);
        /// <summary>HTML デコードした文字列を返す。null の場合は null を返す。</summary>
        public static string? HtmlDecode(this string? value) => value == null ? null : HttpUtility.HtmlDecode(value);

        /// <summary>改行コード（\r\n / \r / \n）を &lt;br /&gt; タグに置換した文字列を返す。</summary>
        public static string GetBrText(this string? value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\r\n", "<br />").Replace("\r", "<br />").Replace("\n", "<br />");
        }

        /// <summary>指定したすべての文字列を含む場合に true を返す。</summary>
        public static bool ContainsAll(this string source, params string[] values)
        {
            return values.All(v => source.Contains(v));
        }

        /// <summary>指定した文字列のいずれかを含む場合に true を返す。</summary>
        public static bool ContainsAny(this string source, params string[] values)
        {
            return values.Any(v => source.Contains(v));
        }

        /// <summary>先頭から最大 length 文字を返す。</summary>
        public static string Left(this string s, int length) => s.Length <= length ? s : s.Substring(0, length);
        /// <summary>start 位置から最大 length 文字を返す。</summary>
        public static string Mid(this string s, int start, int length) => s.Substring(start, Math.Min(length, s.Length - start));
        /// <summary>末尾から最大 length 文字を返す。</summary>
        public static string Right(this string s, int length) => s.Length <= length ? s : s.Substring(s.Length - length);
    }
}
